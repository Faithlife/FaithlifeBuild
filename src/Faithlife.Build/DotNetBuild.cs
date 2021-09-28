using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;
using LibGit2Sharp;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using static Faithlife.Build.AppRunner;
using static Faithlife.Build.BuildUtility;
using static Faithlife.Build.DotNetRunner;
using static Faithlife.Build.MSBuildRunner;
using Repository = LibGit2Sharp.Repository;

namespace Faithlife.Build
{
	/// <summary>
	/// Supports .NET builds.
	/// </summary>
	public static class DotNetBuild
	{
		/// <summary>
		/// Adds the standard .NET targets to the build.
		/// </summary>
		/// <param name="build">The build to which to add targets.</param>
		/// <param name="settings">The build settings.</param>
		public static void AddDotNetTargets(this BuildApp build, DotNetBuildSettings? settings = null)
		{
			settings ??= new DotNetBuildSettings();

			var buildOptions = settings.BuildOptions ??= new DotNetBuildOptions();
			buildOptions.ConfigurationOption ??= build.AddOption("-c|--configuration <name>", "The configuration to build (default Release)", "Release");
			buildOptions.PlatformOption ??= build.AddOption("-p|--platform <name>", "The solution platform to build");
			buildOptions.VerbosityOption ??= build.AddOption("-v|--verbosity <level>", "The build verbosity (q[uiet], m[inimal], n[ormal], d[etailed])");
			buildOptions.VersionSuffixOption ??= build.AddOption("--version-suffix <suffix>", "Generates a prerelease package");
			buildOptions.NuGetOutputOption ??= build.AddOption("--nuget-output <path>", "Directory for generated package (default release)", "release");
			buildOptions.TriggerOption ??= build.AddOption("--trigger <name>", "The git branch or tag that triggered the build");
			buildOptions.BuildNumberOption ??= build.AddOption("--build-number <number>", "The automated build number");
			buildOptions.NoTestFlag ??= build.AddFlag("--no-test", "Skip the unit tests");

			var solutionName = settings.SolutionName;
			var nugetSource = settings.NuGetSource ?? "https://api.nuget.org/v3/index.json";
			var msbuildSettings = settings.MSBuildSettings;

			IReadOnlyList<string>? packagePaths = default;

			build.Target("clean")
				.Describe("Deletes all build output")
				.Does(() =>
				{
					var findDirectoriesToDelete = settings.CleanSettings?.FindDirectoriesToDelete ??
						(() => FindDirectories("{src,tests,tools}/**/{bin,obj}").Except(FindDirectories("tools/bin")).ToList());
					foreach (var directoryToDelete in findDirectoriesToDelete())
						DeleteDirectory(directoryToDelete);

					var extraProperties = settings.GetExtraPropertyArgs("clean");
					if (msbuildSettings is null)
						RunDotNet(new[] { "clean", solutionName, "-c", settings.GetConfiguration(), settings.GetPlatformArg(), settings.GetVerbosityArg(), settings.GetMaxCpuCountArg() }.Concat(extraProperties));
					else
						MSBuild(new[] { solutionName, "-t:Clean", settings.GetConfigurationArg(), settings.GetPlatformArg(), settings.GetVerbosityArg(), settings.GetMaxCpuCountArg() }.Concat(extraProperties));
				});

			build.Target("restore")
				.Describe("Restores NuGet packages")
				.Does(() =>
				{
					var extraProperties = settings.GetExtraPropertyArgs("restore");
					if (msbuildSettings is null)
						RunDotNet(new[] { "restore", solutionName, settings.GetPlatformArg(), settings.GetVerbosityArg(), settings.GetMaxCpuCountArg() }.Concat(extraProperties));
					else
						MSBuild(new[] { solutionName, "-t:Restore", settings.GetConfigurationArg(), settings.GetPlatformArg(), settings.GetVerbosityArg(), settings.GetMaxCpuCountArg() }.Concat(extraProperties));

					if (DotNetLocalTool.Any())
						RunDotNet("tool", "restore");
				});

			build.Target("build")
				.DependsOn("restore")
				.Describe("Builds the solution")
				.Does(() =>
				{
					var extraProperties = settings.GetExtraPropertyArgs("build");
					if (msbuildSettings is null)
						RunDotNet(new[] { "build", solutionName, "-c", settings.GetConfiguration(), settings.GetPlatformArg(), settings.GetBuildNumberArg(), "--no-restore", settings.GetVerbosityArg(), settings.GetMaxCpuCountArg(), settings.GetBuildSummaryArg() }.Concat(extraProperties));
					else
						MSBuild(new[] { solutionName, settings.GetConfigurationArg(), settings.GetPlatformArg(), settings.GetBuildNumberArg(), settings.GetVerbosityArg(), settings.GetMaxCpuCountArg(), settings.GetBuildSummaryArg() }.Concat(extraProperties));
				});

			build.Target("test")
				.DependsOn("build")
				.Describe("Runs the unit tests")
				.Does(() =>
				{
					if (buildOptions.NoTestFlag!.Value)
					{
						Console.WriteLine("Skipping unit tests due to --no-test.");
					}
					else
					{
						IReadOnlyList<string?> testPaths;

						if (settings.TestSettings?.FindAssemblies is var findAssemblies and not null)
							testPaths = findAssemblies(settings);
#pragma warning disable 618
						else if (settings.TestSettings?.FindTestAssemblies is var findTestAssemblies and not null)
							testPaths = findTestAssemblies();
#pragma warning restore 618
						else if (settings.TestSettings?.FindProjects is var findTestProjects and not null)
							testPaths = findTestProjects();
						else
							testPaths = new[] { solutionName };

						settings.RunTests(testPaths);
					}
				});

			build.Target("package")
				.DependsOn("test")
				.Describe("Creates NuGet packages")
				.Does(() =>
				{
					packagePaths = BuildNuGetPackages();
				});

			(string? Trigger, bool AutoDetected) GetTrigger()
			{
				var trigger = buildOptions.TriggerOption!.Value;

				if (trigger == "detect")
				{
					using var repository = new Repository(".");
					var headSha = repository.Head.Tip.Sha;
					var autoTrigger = GetBestTriggerFromTags(repository.Tags.Where(x => x.Target.Sha == headSha).Select(x => x.FriendlyName).ToList());
					if (autoTrigger is not null)
					{
						Console.WriteLine($"Detected trigger: {trigger}");
						return (autoTrigger, true);
					}
				}

				return (trigger, false);
			}

			IReadOnlyList<string> BuildNuGetPackages()
			{
				var (trigger, _) = GetTrigger();

				var versionSuffix = buildOptions.VersionSuffixOption!.Value;
				if (versionSuffix is null && trigger is not null)
					versionSuffix = GetVersionFromTrigger(trigger) is string triggerVersion ? SplitVersion(triggerVersion).Suffix : null;

				var nugetOutputPath = Path.GetFullPath(buildOptions.NuGetOutputOption!.Value!);
				var tempOutputPath = Path.Combine(nugetOutputPath, Path.GetRandomFileName());

				var packageProjects = new List<string?>();

				var findPackageProjects = settings.PackageSettings?.FindProjects;
				if (findPackageProjects is not null)
					packageProjects.AddRange(findPackageProjects());
				else
					packageProjects.Add(solutionName);

				var extraProperties = settings.GetExtraPropertyArgs("package").ToList();
				foreach (var packageProject in packageProjects)
				{
					if (msbuildSettings is null)
					{
						RunDotNet(new[]
						{
							"pack", packageProject,
							"-c", settings.GetConfiguration(),
							settings.GetPlatformArg(),
							"--no-build",
							"--output", tempOutputPath,
							versionSuffix is not null ? "--version-suffix" : null, versionSuffix,
							settings.GetMaxCpuCountArg(),
						}.Concat(extraProperties));
					}
					else
					{
						MSBuild(new[]
						{
							packageProject, "-t:Pack",
							settings.GetConfigurationArg(),
							settings.GetPlatformArg(),
							"-p:NoBuild=true",
							$"-p:PackageOutputPath={tempOutputPath}",
							versionSuffix is not null ? $"-p:VersionSuffix={versionSuffix}" : null,
							settings.GetVerbosityArg(),
							settings.GetMaxCpuCountArg(),
						}.Concat(extraProperties));
					}
				}

				var createdPackagePaths = new List<string>();

				var tempPackagePaths = FindFilesFrom(tempOutputPath, "*.nupkg");
				foreach (var tempPackagePath in tempPackagePaths)
				{
					var packagePath = Path.Combine(nugetOutputPath, Path.GetFileName(tempPackagePath) ?? throw new InvalidOperationException());
					if (File.Exists(packagePath))
						File.Delete(packagePath);
					File.Move(tempPackagePath, packagePath);
					createdPackagePaths.Add(packagePath);
					Console.WriteLine($"NuGet package: {packagePath}");
				}
				DeleteDirectory(tempOutputPath);

				if (createdPackagePaths.Count == 0)
					throw new BuildException("No NuGet packages created.");

				return createdPackagePaths;
			}

			build.Target("publish")
				.Describe("Publishes NuGet packages and documentation")
				.DependsOn("package")
				.Does(() =>
				{
					// we must build the packages to identify them
					packagePaths ??= BuildNuGetPackages();

					if (packagePaths.Count == 0)
						throw new BuildException("No NuGet packages found.");

					var (trigger, triggerAutoDetected) = GetTrigger();

					if (trigger is null)
					{
						if (packagePaths.Any(x => GetPackageInfo(x).Version == "0.0.0"))
						{
							Console.WriteLine("Not publishing package with version 0.0.0. Change package version to publish.");
							return;
						}

						trigger = "publish-all";
					}

					var triggerParts = trigger.Split('-');
					var publishTrigger = triggerParts.Length >= 2 && triggerParts[0] == "publish" ? triggerParts[1] : null;
					var shouldPublishPackages = publishTrigger == "package" || publishTrigger == "packages" || publishTrigger == "all";
					var shouldPublishDocs = publishTrigger == "docs" || publishTrigger == "all";
					var shouldSkipDuplicates = publishTrigger == "all";

					var triggerVersion = GetVersionFromTrigger(trigger);
					if (triggerVersion is not null)
					{
						var mismatches = packagePaths.Where(x => GetPackageInfo(x).Version != triggerVersion).ToList();
						if (mismatches.Count != 0)
							throw new BuildException($"Trigger '{trigger}' doesn't match package version: {string.Join(", ", mismatches.Select(Path.GetFileName))}");

						shouldPublishPackages = true;
#pragma warning disable CA1307 // Specify StringComparison for clarity
						shouldPublishDocs = !triggerVersion.Contains('-');
#pragma warning restore CA1307 // Specify StringComparison for clarity
					}

					if (shouldPublishPackages || shouldPublishDocs)
					{
						var docsSettings = settings.DocsSettings;
						var shouldPushDocs = false;
						string? docsCloneDirectory = null;
						string? docsRepoDirectory = null;
						string? docsGitBranchName = null;
						string? tagsCloneDirectory = null;

						if (shouldPublishDocs && docsSettings is not null)
						{
							if (docsSettings.GitLogin is null || docsSettings.GitAuthor is null)
								throw new BuildException("GitLogin and GitAuthor must be set to publish documentation.");

							var gitRepositoryUrl = docsSettings.GitRepositoryUrl;
							docsGitBranchName = docsSettings.GitBranchName;

							if (gitRepositoryUrl is not null)
							{
								docsCloneDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
								Repository.Clone(sourceUrl: gitRepositoryUrl, workdirPath: docsCloneDirectory,
									options: new CloneOptions { BranchName = docsGitBranchName, CredentialsProvider = ProvideDocsCredentials });
								docsRepoDirectory = docsCloneDirectory;
							}
							else
							{
								docsRepoDirectory = ".";
							}

							using var repository = new Repository(docsRepoDirectory);
							if (gitRepositoryUrl is not null)
							{
								docsGitBranchName ??= repository.Head.FriendlyName;
							}
							else if (docsGitBranchName is not null)
							{
								if (docsGitBranchName != repository.Head.FriendlyName)
								{
									var branch = repository.Branches[docsGitBranchName] ?? repository.CreateBranch(docsGitBranchName);
									Commands.Checkout(repository, branch);
								}
							}
							else
							{
								var branch = repository.Branches.FirstOrDefault(x => x.IsCurrentRepositoryHead);
								if (branch is null)
								{
									var autoBranchName = Environment.GetEnvironmentVariable("APPVEYOR_REPO_BRANCH");

									if (autoBranchName is null)
									{
										var gitRef = Environment.GetEnvironmentVariable("GITHUB_REF");
										const string prefix = "refs/heads/";
										if (gitRef is not null && gitRef.StartsWith(prefix, StringComparison.Ordinal))
											autoBranchName = gitRef.Substring(prefix.Length);
									}

									if (autoBranchName is not null)
										branch = repository.Branches[autoBranchName] ?? repository.CreateBranch(autoBranchName);
									else
										branch = repository.Branches.FirstOrDefault(x => x.Tip.Sha == repository.Head.Tip.Sha);

									if (branch is not null)
										Commands.Checkout(repository, branch);
								}
								if (branch is null)
									throw new BuildException("Could not determine repository branch for publishing docs.");
								docsGitBranchName = branch.FriendlyName;
							}

							var docsPath = Path.Combine(docsRepoDirectory, docsSettings.TargetDirectory ?? "docs");

							string? xmlDocGenPath = null;
							var xmlDocGenProject = FindFiles("tools/XmlDocGen/XmlDocGen.csproj").FirstOrDefault();
							if (xmlDocGenProject is not null)
							{
								RunDotNet("publish", xmlDocGenProject,
									"-c", settings.GetConfiguration(),
									settings.GetPlatformArg(),
									"--nologo",
									"--verbosity", "quiet",
									"--output", Path.Combine("tools", "bin", "XmlDocGen"));
								xmlDocGenPath = Path.Combine("tools", "bin", "XmlDocGen", "XmlDocGen.dll");
								if (!File.Exists(xmlDocGenPath))
									xmlDocGenPath = Path.Combine("tools", "bin", "XmlDocGen", "XmlDocGen.exe");
								if (!File.Exists(xmlDocGenPath))
									throw new BuildException("Failed to build XmlDocGen.");
							}

							var projectHasDocs = docsSettings.ProjectHasDocs ?? (_ => true);
							foreach (var project in packagePaths.Select(GetPackageInfo).Where(x => projectHasDocs(x.Name)))
							{
								var assemblyPaths = new List<string>();
								if (docsSettings.FindAssemblies is not null)
								{
									assemblyPaths.AddRange(docsSettings.FindAssemblies(project.Name));
								}
								else if (docsSettings.FindAssembly is not null)
								{
									var assemblyPath = docsSettings.FindAssembly(project.Name);
									if (assemblyPath is not null)
										assemblyPaths.Add(assemblyPath);
								}
								else if (xmlDocGenPath is not null)
								{
									assemblyPaths.Add(project.Name);
								}
								else
								{
									var assemblyPath =
										FindFiles($"tools/XmlDocTarget/bin/**/{project.Name}.dll").OrderByDescending(File.GetLastWriteTime).FirstOrDefault() ??
										FindFiles($"src/{project.Name}/bin/**/{project.Name}.dll").OrderByDescending(File.GetLastWriteTime).FirstOrDefault();
									if (assemblyPath is not null)
										assemblyPaths.Add(assemblyPath);
								}

								if (assemblyPaths.Count != 0)
								{
									if (xmlDocGenPath is not null)
									{
										var isDotNetApp = string.Equals(Path.GetExtension(xmlDocGenPath), ".dll", StringComparison.OrdinalIgnoreCase);
										foreach (var assemblyPath in assemblyPaths)
										{
											if (isDotNetApp)
												RunDotNet(new[] { xmlDocGenPath }.Concat(GetXmlDocArgs(assemblyPath)));
											else
												RunApp(xmlDocGenPath, new AppRunnerSettings { Arguments = GetXmlDocArgs(assemblyPath), IsFrameworkApp = true });
										}
									}
									else if (DotNetLocalTool.TryCreate("xmldocmd") is DotNetLocalTool xmldocmd)
									{
										foreach (var assemblyPath in assemblyPaths)
											xmldocmd.Run(GetXmlDocArgs(assemblyPath));
									}
									else
									{
#pragma warning disable 618
										var dotNetTools = settings.DotNetTools ?? new DotNetTools(Path.Combine("tools", "bin"));
										var xmlDocMarkdownVersion = settings.DocsSettings?.ToolVersion ?? "2.0.1";

										foreach (var assemblyPath in assemblyPaths)
											RunApp(dotNetTools.GetToolPath($"xmldocmd/{xmlDocMarkdownVersion}"), GetXmlDocArgs(assemblyPath));
#pragma warning restore 618
									}
								}
								else
								{
									Console.WriteLine($"Documentation not generated for {project.Name}; assembly not found.");
								}

								string?[] GetXmlDocArgs(string input) =>
									new[] { input, docsPath, "--source", $"{docsSettings!.SourceCodeUrl}/{project.Name}", "--newline", "lf", "--clean", string.IsNullOrEmpty(project.Suffix) ? null : "--dryrun" };
							}

							shouldPushDocs = repository.RetrieveStatus().IsDirty;
						}

						if (shouldPublishPackages)
						{
							var nugetApiKey = settings.NuGetApiKey;
							if (string.IsNullOrEmpty(nugetApiKey))
								throw new BuildException("NuGetApiKey required to publish.");

							if (triggerAutoDetected)
							{
								var nugetSettings = Settings.LoadDefaultSettings(root: null);
								var packageSourceProvider = new PackageSourceProvider(nugetSettings);
								var sourceRepositoryProvider = new SourceRepositoryProvider(packageSourceProvider, NuGet.Protocol.Core.Types.Repository.Provider.GetCoreV3());
								using var sourceCacheContext = new SourceCacheContext();
								var nugetRepositories = sourceRepositoryProvider.GetRepositories()
									.Select(x => x.GetResourceAsync<DependencyInfoResource>().GetAwaiter().GetResult())
									.ToList();

								var alreadyPushedPackages = new List<string>();
								foreach (var packagePath in packagePaths.ToList())
								{
									var packageInfo = GetPackageInfo(packagePath);
									var package = new PackageIdentity(packageInfo.Name, NuGetVersion.Parse(packageInfo.Version));

									foreach (var nugetRepository in nugetRepositories)
									{
										var dependencyInfo = nugetRepository.ResolvePackage(package, NuGetFramework.AnyFramework,
											sourceCacheContext, NullLogger.Instance, CancellationToken.None).GetAwaiter().GetResult();
										if (dependencyInfo is not null)
										{
											Console.WriteLine($"Package already pushed: {packageInfo.Name} {packageInfo.Version}");
											alreadyPushedPackages.Add(packagePath);
											break;
										}
									}
								}

								if (alreadyPushedPackages.Count != 0)
									packagePaths = packagePaths.Except(alreadyPushedPackages).ToList();
							}

							var tagsToPush = new HashSet<string>();
							var packageSettings = settings.PackageSettings;
							var pushSuccess = false;

							foreach (var packagePath in packagePaths)
							{
								var pushArgs = new[]
								{
									"nuget", "push", packagePath,
									"--source", nugetSource,
									"--api-key", nugetApiKey,
									shouldSkipDuplicates ? "--skip-duplicate" : null,
								};

								var skippedDuplicate = false;

								RunDotNet(new AppRunnerSettings
								{
									Arguments = pushArgs,
									HandleOutputLine = line =>
									{
										Console.WriteLine(line);
										if (line.TrimStart().StartsWith("Conflict", StringComparison.Ordinal))
											skippedDuplicate = true;
									},
								});

								if (!skippedDuplicate)
								{
									pushSuccess = true;

									if (packageSettings?.PushTagOnPublish is var getTag and not null &&
										getTag(GetPackageInfo(packagePath)) is string tag &&
										tag.Length != 0)
									{
										tagsToPush.Add(tag);
									}
								}
							}

							if (tagsToPush.Count != 0)
							{
								if (packageSettings?.GitLogin is null)
									throw new BuildException("GitLogin must be set to push tags.");

								var commitSha = GetGitCommitSha();

								string tagsRepoDirectory = ".";
								var gitRepositoryUrl = packageSettings.GitRepositoryUrl;
								if (gitRepositoryUrl is not null)
								{
									tagsCloneDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
									Repository.Clone(sourceUrl: gitRepositoryUrl, workdirPath: tagsCloneDirectory,
										options: new CloneOptions { CredentialsProvider = ProvidePackageTagCredentials });
									tagsRepoDirectory = tagsCloneDirectory;
								}

								using var repository = new Repository(tagsRepoDirectory);
								foreach (var tagToPush in tagsToPush)
								{
									Console.WriteLine($"Pushing git tag {tagToPush} at {commitSha}.");
									repository.ApplyTag(tagName: tagToPush, objectish: commitSha);
									repository.Network.Push(
										remote: repository.Network.Remotes["origin"],
										pushRefSpec: $"refs/tags/{tagToPush}",
										pushOptions: new PushOptions { CredentialsProvider = ProvidePackageTagCredentials });
								}

								Credentials ProvidePackageTagCredentials(string url, string usernameFromUrl, SupportedCredentialTypes types) =>
									new UsernamePasswordCredentials { Username = packageSettings!.GitLogin!.Username, Password = packageSettings!.GitLogin!.Password };
							}

							// don't push documentation if the packages have already been published
							if (!pushSuccess)
								shouldPushDocs = false;
						}

						if (shouldPushDocs)
						{
							using var repository = new Repository(docsRepoDirectory);
							Console.WriteLine("Publishing documentation changes.");
							Commands.Stage(repository, "*");
							var author = new Signature(docsSettings!.GitAuthor!.Name, docsSettings!.GitAuthor!.Email, DateTimeOffset.Now);
							repository.Commit("Documentation updated.", author, author, new CommitOptions());
							repository.Network.Push(
								remote: repository.Network.Remotes["origin"],
								pushRefSpec: $"refs/heads/{docsGitBranchName}",
								pushOptions: new PushOptions { CredentialsProvider = ProvideDocsCredentials });
						}

						if (docsCloneDirectory is not null)
							ForceDeleteDirectory(docsCloneDirectory);

						if (tagsCloneDirectory is not null)
							ForceDeleteDirectory(tagsCloneDirectory);

						Credentials ProvideDocsCredentials(string url, string usernameFromUrl, SupportedCredentialTypes types) =>
							new UsernamePasswordCredentials { Username = docsSettings!.GitLogin!.Username, Password = docsSettings!.GitLogin!.Password };

						static void ForceDeleteDirectory(string path)
						{
							try
							{
								DeleteDirectory(path);
							}
							catch (UnauthorizedAccessException)
							{
#if NETSTANDARD2_0
								var options = SearchOption.AllDirectories;
#else
								var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };
#endif
								foreach (var fileInfo in new DirectoryInfo(path).EnumerateFiles("*", options).Where(x => x.IsReadOnly))
									fileInfo.IsReadOnly = false;
								DeleteDirectory(path);
							}
						}
					}
					else
					{
						Console.WriteLine("To publish to NuGet, push this tag: v" + GetPackageInfo(packagePaths[0]).Version);
					}
				});

			if (DotNetLocalTool.TryCreate("dotnet-format") is DotNetLocalTool dotnetFormat)
			{
				build.Target("format")
					.DependsOn("restore")
					.Describe("Fixes coding style with dotnet-format")
					.Does(() =>
					{
						dotnetFormat.Run(settings.GetVerbosityArg());
					});
			}

			if (DotNetLocalTool.TryCreate("jetbrains.resharper.globaltools") is DotNetLocalTool jb)
			{
				build.Target("cleanup")
					.DependsOn("restore")
					.Describe("Fixes coding style with JetBrains CleanupCode")
					.Does(() =>
					{
						jb.Run(
							new[]
							{
								"cleanupcode",
								"--profile=Build",
								"--verbosity=ERROR",
								"--disable-settings-layers:GlobalAll;GlobalPerProduct;SolutionPersonal;ProjectPersonal",
							}.Concat(GetJetBrainsProperties()).Append(GetSolutionName()));
					});

				build.Target("inspect")
					.DependsOn("restore")
					.Describe("Checks coding style with JetBrains InspectCode")
					.Does(() =>
					{
						var outputPath = Path.Combine("release", "inspect.xml");

						jb.Run(
							new[]
							{
								"inspectcode",
								"--severity=WARNING",
								"--verbosity=ERROR",
								"--format=Xml",
								"--disable-settings-layers:GlobalAll;GlobalPerProduct;SolutionPersonal;ProjectPersonal",
								$"--output={outputPath}",
							}.Concat(GetJetBrainsProperties()).Append(GetSolutionName()));

						var outputDocument = XDocument.Load(outputPath);
						var issueElements = outputDocument.XPathSelectElements("//Issue").ToList();
						foreach (var issueElement in issueElements)
							Console.WriteLine($"{issueElement.Attribute("File")!.Value}({issueElement.Attribute("Line")!.Value}): {issueElement.Attribute("Message")!.Value}");
						if (issueElements.Count != 0)
							throw new BuildException($"{issueElements.Count} inspection issues found.");
					});

				string GetSolutionName()
				{
					if (solutionName is not null)
						return solutionName;

					var solutionNames = FindFiles("*.sln");
					if (solutionNames.Count == 0)
						throw new BuildException("Solution file not found.");
					if (solutionNames.Count > 1)
						throw new BuildException("Multiple solution files found.");
					return solutionNames[0];
				}

				IEnumerable<string?> GetJetBrainsProperties()
				{
					yield return $"--properties:Configuration={settings.GetConfiguration()}";
					yield return settings.GetPlatform() is string platform ? $"--properties:Platform={platform}" : null;
				}
			}

			void MSBuild(IEnumerable<string?> arguments) => RunMSBuild(msbuildSettings, arguments!);

			string GetGitCommitSha()
			{
				using var repository = new Repository(".");
				return repository.Head.Tip.Sha;
			}
		}

		/// <summary>
		/// Gets the configuration.
		/// </summary>
		public static string GetConfiguration(this DotNetBuildSettings settings) =>
			settings!.BuildOptions!.ConfigurationOption!.Value!;

		/// <summary>
		/// Gets the MSBuild-style argument that specifies the configuration.
		/// </summary>
		public static string GetConfigurationArg(this DotNetBuildSettings settings) => $"-p:Configuration={settings.GetConfiguration()}";

		/// <summary>
		/// Gets the platform, if any.
		/// </summary>
		public static string? GetPlatform(this DotNetBuildSettings settings) =>
			settings.BuildOptions!.PlatformOption!.Value ?? settings.SolutionPlatform;

		/// <summary>
		/// Gets the argument that specifies the platform, if needed.
		/// </summary>
		public static string? GetPlatformArg(this DotNetBuildSettings settings) =>
			settings.GetPlatform() is string platform ? $"-p:Platform={platform}" : null;

		/// <summary>
		/// Gets the argument that specifies the maximum CPU count.
		/// </summary>
		public static string GetMaxCpuCountArg(this DotNetBuildSettings settings) =>
			settings.MaxCpuCount is not null ? $"-maxcpucount:{settings.MaxCpuCount}" : "-maxcpucount";

		/// <summary>
		/// Gets the build verbosity.
		/// </summary>
		/// <remarks>Defaults to minimal.</remarks>
		public static DotNetBuildVerbosity GetVerbosity(this DotNetBuildSettings settings) =>
			settings.BuildOptions!.VerbosityOption!.Value?.ToLowerInvariant() switch
			{
				"q" => DotNetBuildVerbosity.Quiet,
				"quiet" => DotNetBuildVerbosity.Quiet,
				"m" => DotNetBuildVerbosity.Minimal,
				"minimal" => DotNetBuildVerbosity.Minimal,
				"n" => DotNetBuildVerbosity.Normal,
				"normal" => DotNetBuildVerbosity.Normal,
				"d" => DotNetBuildVerbosity.Detailed,
				"detailed" => DotNetBuildVerbosity.Detailed,
				"diag" => DotNetBuildVerbosity.Diagnostic,
				"diagnostic" => DotNetBuildVerbosity.Diagnostic,
				null => settings!.Verbosity ?? DotNetBuildVerbosity.Minimal,
				_ => throw new BuildException($"Unexpected verbosity option: {settings.BuildOptions.VerbosityOption.Value}"),
			};

		/// <summary>
		/// Gets the argument that specifies the verbosity.
		/// </summary>
		/// <remarks>Defaults to minimal.</remarks>
		public static string GetVerbosityArg(this DotNetBuildSettings settings)
		{
			var verbosity = settings.GetVerbosity();

			var argument = verbosity switch
			{
				DotNetBuildVerbosity.Quiet => "quiet",
				DotNetBuildVerbosity.Minimal => "minimal",
				DotNetBuildVerbosity.Normal => "normal",
				DotNetBuildVerbosity.Detailed => "detailed",
				DotNetBuildVerbosity.Diagnostic => "diagnostic",
				_ => throw new BuildException($"Unexpected DotNetBuildVerbosity: {verbosity}"),
			};

			return $"-v:{argument}";
		}

		/// <summary>
		/// Gets the build number, if any.
		/// </summary>
		/// <remarks>If not specified on the command line or in the settings, the environment
		/// variables used by Appveyor, GitHub Actions, and Jenkins will be used, if set.</remarks>
		public static string? GetBuildNumber(this DotNetBuildSettings settings) =>
			settings.BuildOptions!.BuildNumberOption!.Value ??
			settings.BuildNumber ??
			Environment.GetEnvironmentVariable("APPVEYOR_BUILD_NUMBER") ??
			Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER") ??
			Environment.GetEnvironmentVariable("BUILD_NUMBER");

		/// <summary>
		/// Gets the argument that specifies the build number.
		/// </summary>
		public static string? GetBuildNumberArg(this DotNetBuildSettings settings) =>
			settings.GetBuildNumber() is string buildNumber ? $"-p:BuildNumber={buildNumber}" : null;

		/// <summary>
		/// Gets the argument that specifies whether a build summary should be output.
		/// </summary>
		public static string GetBuildSummaryArg(this DotNetBuildSettings settings) => settings.ShowSummary.GetValueOrDefault() ? "-clp:Summary" : "-clp:NoSummary";

		/// <summary>
		/// Gets extra properties for the specified target.
		/// </summary>
		public static IEnumerable<string> GetExtraPropertyArgs(this DotNetBuildSettings settings, string target)
		{
			var pairs = settings.ExtraProperties?.Invoke(target);
			if (pairs is not null)
			{
				foreach (var (key, value) in pairs)
					yield return $"-p:{key}={value}";
			}
		}

		/// <summary>
		/// Runs tests on the specified paths.
		/// </summary>
		/// <remarks>Calls <c>RunTests</c> on each path, in parallel if <c>UseParallel</c> is true.</remarks>
		public static void RunTests(this DotNetBuildSettings settings, IEnumerable<string?> paths)
		{
			if (settings.TestSettings?.UseParallel == true)
			{
				Parallel.ForEach(paths,
					path => settings.RunTests(path));
			}
			else
			{
				foreach (var path in paths)
					settings.RunTests(path);
			}
		}

		/// <summary>
		/// Runs tests on the specified path.
		/// </summary>
		/// <remarks>If null, runs all tests in the current solution. If an assembly, runs all tests in that assembly.
		/// Otherwise, runs all tests in the specified project or solution.</remarks>
		public static void RunTests(this DotNetBuildSettings settings, string? path)
		{
			if (settings.TestSettings?.RunTests is not null)
			{
				settings.TestSettings.RunTests(path);
			}
			else
			{
				var extension = Path.GetExtension(path)?.ToLowerInvariant();
				if (extension == ".dll" || extension == ".exe")
					RunDotNet(new AppRunnerSettings { Arguments = new[] { "test", Path.GetFileName(path) }, WorkingDirectory = Path.GetDirectoryName(path) });
				else
					RunDotNet(new[] { "test", path, "-c", settings.GetConfiguration(), settings.GetPlatformArg(), "--no-build", settings.GetMaxCpuCountArg() }.Concat(settings.GetExtraPropertyArgs("test")));
			}
		}

		/// <summary>
		/// Gets extra properties for the specified target.
		/// </summary>
		[Obsolete("Use other overload.")]
		public static IEnumerable<string> GetExtraPropertyArgs(string target, DotNetBuildSettings settings) => settings.GetExtraPropertyArgs(target);

		private static DotNetPackageInfo GetPackageInfo(string path)
		{
			var match = Regex.Match(path, @"[/\\](?<name>[^/\\]+)\.(?<version>[0-9]+\.[0-9]+\.[0-9]+(-(?<suffix>.+))?)\.nupkg$", RegexOptions.ExplicitCapture);
			return new DotNetPackageInfo(name: match.Groups["name"].Value, version: match.Groups["version"].Value, suffix: match.Groups["suffix"].Value);
		}

		private static string? GetVersionFromTrigger(string trigger)
		{
			var version = Regex.Match(trigger, @"^v(?<version>[0-9]+\.[0-9]+\.[0-9]+(-.+)?)$").Groups["version"].Value;
			return version.Length != 0 ? version : null;
		}

		private static (int Major, int Minor, int Patch, string Suffix) SplitVersion(string version)
		{
			var hyphenParts = version.Split(new[] { '-' }, 2);
			var dotParts = hyphenParts[0].Split(new[] { '.' }, 3);
			return (int.Parse(dotParts[0], CultureInfo.InvariantCulture), int.Parse(dotParts[1], CultureInfo.InvariantCulture), int.Parse(dotParts[2], CultureInfo.InvariantCulture), hyphenParts.ElementAtOrDefault(1));
		}

		private static string? GetBestTriggerFromTags(IReadOnlyList<string> tags) =>
			tags
				.Select(x => (Tag: x, Version: GetVersionFromTrigger(x)))
				.Where(x => x.Version is not null)
				.Select(x => (x.Tag, Version: SplitVersion(x.Version!)))
				.OrderByDescending(x => x.Version.Major)
				.ThenByDescending(x => x.Version.Minor)
				.ThenByDescending(x => x.Version.Patch)
				.ThenByDescending(x => string.IsNullOrEmpty(x.Version.Suffix))
				.ThenByDescending(x => x.Version.Suffix, StringComparer.Ordinal)
				.Select(x => x.Tag)
				.Concat(tags.Where(x => x.StartsWith("publish-", StringComparison.Ordinal)))
				.FirstOrDefault();
	}
}
