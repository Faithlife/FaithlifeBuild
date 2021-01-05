using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
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
			var configurationOption = buildOptions.ConfigurationOption ??=
				build.AddOption("-c|--configuration <name>", "The configuration to build (default Release)", "Release");
			var platformOption = buildOptions.PlatformOption ??=
				build.AddOption("-p|--platform <name>", "The solution platform to build");
			var verbosityOption = buildOptions.VerbosityOption ??=
				build.AddOption("-v|--verbosity <level>", "The build verbosity (q[uiet], m[inimal], n[ormal], d[etailed])");
			var versionSuffixOption = buildOptions.VersionSuffixOption ??=
				build.AddOption("--version-suffix <suffix>", "Generates a prerelease package");
			var nugetOutputOption = buildOptions.NuGetOutputOption ??=
				build.AddOption("--nuget-output <path>", "Directory for generated package (default release)", "release");
			var triggerOption = buildOptions.TriggerOption ??=
				build.AddOption("--trigger <name>", "The git branch or tag that triggered the build");
			var buildNumberOption = buildOptions.BuildNumberOption ??=
				build.AddOption("--build-number <number>", "The automated build number");
			var noTestFlag = buildOptions.NoTestFlag ??=
				build.AddFlag("--no-test", "Skip the unit tests");

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

					var verbosity = GetVerbosity();
					var extraProperties = GetExtraProperties("clean");
					if (msbuildSettings is null)
						RunDotNet(new[] { "clean", solutionName, "-c", configurationOption.Value, GetPlatformArg(), "--verbosity", verbosity, GetMaxCpuCountArg() }.Concat(extraProperties));
					else
						MSBuild(new[] { solutionName, "-t:Clean", $"-p:Configuration={configurationOption.Value}", GetPlatformArg(), $"-v:{verbosity}", GetMaxCpuCountArg() }.Concat(extraProperties));
				});

			build.Target("restore")
				.Describe("Restores NuGet packages")
				.Does(() =>
				{
					var verbosity = GetVerbosity();
					var extraProperties = GetExtraProperties("restore");
					if (msbuildSettings is null)
						RunDotNet(new[] { "restore", solutionName, GetPlatformArg(), "--verbosity", verbosity, GetMaxCpuCountArg() }.Concat(extraProperties));
					else
						MSBuild(new[] { solutionName, "-t:Restore", $"-p:Configuration={configurationOption.Value}", GetPlatformArg(), $"-v:{verbosity}", GetMaxCpuCountArg() }.Concat(extraProperties));

					if (DotNetLocalTool.Any())
						RunDotNet("tool", "restore");
				});

			build.Target("build")
				.DependsOn("restore")
				.Describe("Builds the solution")
				.Does(() =>
				{
					var buildNumberArg = GetBuildNumberArg();

					var verbosity = GetVerbosity();
					var extraProperties = GetExtraProperties("build");
					if (msbuildSettings is null)
						RunDotNet(new[] { "build", solutionName, "-c", configurationOption.Value, GetPlatformArg(), buildNumberArg, "--no-restore", "--verbosity", verbosity, GetMaxCpuCountArg() }.Concat(extraProperties));
					else
						MSBuild(new[] { solutionName, $"-p:Configuration={configurationOption.Value}", GetPlatformArg(), buildNumberArg, $"-v:{verbosity}", GetMaxCpuCountArg() }.Concat(extraProperties));
				});

			build.Target("test")
				.DependsOn("build")
				.Describe("Runs the unit tests")
				.Does(() =>
				{
					if (noTestFlag.Value)
					{
						Console.WriteLine("Skipping unit tests due to --no-test.");
					}
					else
					{
						var extraProperties = GetExtraProperties("test").ToList();
						var findTestAssemblies = settings.TestSettings?.FindTestAssemblies;
						if (findTestAssemblies is not null)
						{
							foreach (var testAssembly in findTestAssemblies())
							{
								if (settings.TestSettings?.RunTests is not null)
									settings.TestSettings.RunTests(testAssembly);
								else
									RunDotNet(new AppRunnerSettings { Arguments = new[] { "test", Path.GetFileName(testAssembly) }.Concat(extraProperties), WorkingDirectory = Path.GetDirectoryName(testAssembly) });
							}
						}
						else
						{
							var testProjects = new List<string?>();

							var findTestProjects = settings.TestSettings?.FindProjects;
							if (findTestProjects is not null)
								testProjects.AddRange(findTestProjects());
							else
								testProjects.Add(solutionName);

							foreach (var testProject in testProjects)
							{
								if (settings.TestSettings?.RunTests is not null)
									settings.TestSettings.RunTests(testProject);
								else
									RunDotNet(new[] { "test", testProject, "-c", configurationOption.Value, GetPlatformArg(), "--no-build", GetMaxCpuCountArg() }.Concat(extraProperties));
							}
						}
					}
				});

			build.Target("package")
				.DependsOn("clean", "test")
				.Describe("Builds NuGet packages")
				.Does(() =>
				{
					packagePaths = BuildNuGetPackages();
				});

			(string? Trigger, bool AutoDetected) GetTrigger()
			{
				var trigger = triggerOption.Value;

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

				var versionSuffix = versionSuffixOption.Value;
				if (versionSuffix is null && trigger is not null)
					versionSuffix = GetVersionFromTrigger(trigger) is string triggerVersion ? SplitVersion(triggerVersion).Suffix : null;

				var nugetOutputPath = Path.GetFullPath(nugetOutputOption.Value!);
				var tempOutputPath = Path.Combine(nugetOutputPath, Path.GetRandomFileName());

				var packageProjects = new List<string?>();

				var findPackageProjects = settings.PackageSettings?.FindProjects;
				if (findPackageProjects is not null)
					packageProjects.AddRange(findPackageProjects());
				else
					packageProjects.Add(solutionName);

				var extraProperties = GetExtraProperties("package").ToList();
				foreach (var packageProject in packageProjects)
				{
					if (msbuildSettings is null)
					{
						RunDotNet(new[]
						{
							"pack", packageProject,
							"-c", configurationOption.Value,
							GetPlatformArg(),
							"--no-build",
							"--output", tempOutputPath,
							versionSuffix is not null ? "--version-suffix" : null, versionSuffix,
							GetMaxCpuCountArg(),
						}.Concat(extraProperties));
					}
					else
					{
						MSBuild(new[]
						{
							packageProject, "-t:Pack",
							$"-p:Configuration={configurationOption.Value}",
							GetPlatformArg(),
							"-p:NoBuild=true",
							$"-p:PackageOutputPath={tempOutputPath}",
							versionSuffix is not null ? $"-p:VersionSuffix={versionSuffix}" : null,
							$"-v:{GetVerbosity()}",
							GetMaxCpuCountArg(),
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
						shouldPublishDocs = triggerVersion.IndexOf('-') == -1;
					}

					if (shouldPublishPackages || shouldPublishDocs)
					{
						var docsSettings = settings.DocsSettings;
						var shouldPushDocs = false;
						string? cloneDirectory = null;
						string? repoDirectory = null;
						string? gitBranchName = null;

						if (shouldPublishDocs && docsSettings is not null)
						{
							if (docsSettings.GitLogin is null || docsSettings.GitAuthor is null)
								throw new BuildException("GitLogin and GitAuthor must be set on DocsSettings.");

							var gitRepositoryUrl = docsSettings.GitRepositoryUrl;
							gitBranchName = docsSettings.GitBranchName;

							if (gitRepositoryUrl is not null)
							{
								cloneDirectory = "docs_repo_" + Path.GetRandomFileName();
								Repository.Clone(sourceUrl: gitRepositoryUrl, workdirPath: cloneDirectory,
									options: new CloneOptions { BranchName = gitBranchName, CredentialsProvider = ProvideCredentials });
								repoDirectory = cloneDirectory;
							}
							else
							{
								repoDirectory = ".";
							}

							using var repository = new Repository(repoDirectory);
							if (gitRepositoryUrl is not null)
							{
								gitBranchName ??= repository.Head.FriendlyName;
							}
							else if (gitBranchName is not null)
							{
								if (gitBranchName != repository.Head.FriendlyName)
								{
									var branch = repository.Branches[gitBranchName] ?? repository.CreateBranch(gitBranchName);
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
								gitBranchName = branch.FriendlyName;
							}

							var docsPath = Path.Combine(repoDirectory, docsSettings.TargetDirectory ?? "docs");

							string? xmlDocGenPath = null;
							var xmlDocGenProject = FindFiles("tools/XmlDocGen/XmlDocGen.csproj").FirstOrDefault();
							if (xmlDocGenProject is not null)
							{
								RunDotNet("publish", xmlDocGenProject, "--output", Path.Combine("tools", "bin", "XmlDocGen"), "--nologo", "--verbosity", "quiet");
								xmlDocGenPath = Path.Combine("tools", "bin", "XmlDocGen", "XmlDocGen.dll");
							}

							var projectHasDocs = docsSettings.ProjectHasDocs ?? (_ => true);
							foreach (var projectName in packagePaths.Select(x => GetPackageInfo(x).Name).Where(projectHasDocs))
							{
								var assemblyPaths = new List<string>();
								if (docsSettings.FindAssemblies is not null)
								{
									assemblyPaths.AddRange(docsSettings.FindAssemblies(projectName));
								}
								else if (docsSettings.FindAssembly is not null)
								{
									var assemblyPath = docsSettings.FindAssembly(projectName);
									if (assemblyPath is not null)
										assemblyPaths.Add(assemblyPath);
								}
								else if (xmlDocGenPath is not null)
								{
									assemblyPaths.Add(projectName);
								}
								else
								{
									var assemblyPath =
										FindFiles($"tools/XmlDocTarget/bin/**/{projectName}.dll").OrderByDescending(File.GetLastWriteTime).FirstOrDefault() ??
										FindFiles($"src/{projectName}/bin/**/{projectName}.dll").OrderByDescending(File.GetLastWriteTime).FirstOrDefault();
									if (assemblyPath is not null)
										assemblyPaths.Add(assemblyPath);
								}

								if (assemblyPaths.Count != 0)
								{
									if (xmlDocGenPath is not null)
									{
										foreach (var assemblyPath in assemblyPaths)
											RunDotNet(new[] { xmlDocGenPath }.Concat(GetXmlDocArgs(assemblyPath)));
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
									Console.WriteLine($"Documentation not generated for {projectName}; assembly not found.");
								}

								string?[] GetXmlDocArgs(string input) =>
									new[] { input, docsPath, "--source", $"{docsSettings!.SourceCodeUrl}/{projectName}", "--newline", "lf", "--clean" };
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
									var (packageName, packageVersion, _) = GetPackageInfo(packagePath);
									var package = new PackageIdentity(packageName, NuGetVersion.Parse(packageVersion));

									foreach (var nugetRepository in nugetRepositories)
									{
										var dependencyInfo = nugetRepository.ResolvePackage(package, NuGetFramework.AnyFramework,
											sourceCacheContext, NullLogger.Instance, CancellationToken.None).GetAwaiter().GetResult();
										if (dependencyInfo is not null)
										{
											Console.WriteLine($"Package already pushed: {packageName} {packageVersion}");
											alreadyPushedPackages.Add(packagePath);
											break;
										}
									}
								}

								if (alreadyPushedPackages.Count != 0)
									packagePaths = packagePaths.Except(alreadyPushedPackages).ToList();
							}

							foreach (var packagePath in packagePaths)
							{
								RunDotNet("nuget", "push", packagePath,
									"--source", nugetSource,
									"--api-key", nugetApiKey,
									shouldSkipDuplicates ? "--skip-duplicate" : null);
							}
						}

						if (shouldPushDocs)
						{
							using var repository = new Repository(repoDirectory);
							Console.WriteLine("Publishing documentation changes.");
							Commands.Stage(repository, "*");
							var author = new Signature(docsSettings!.GitAuthor!.Name, docsSettings!.GitAuthor!.Email, DateTimeOffset.Now);
							repository.Commit("Documentation updated.", author, author, new CommitOptions());
							repository.Network.Push(repository.Network.Remotes["origin"],
								$"refs/heads/{gitBranchName}", new PushOptions { CredentialsProvider = ProvideCredentials });
						}

						if (cloneDirectory is not null)
						{
							// delete the cloned directory
							foreach (var fileInfo in FindFiles(cloneDirectory, "**").Select(x => new FileInfo(x)).Where(x => x.IsReadOnly))
								fileInfo.IsReadOnly = false;
							DeleteDirectory(cloneDirectory);
						}

						Credentials ProvideCredentials(string url, string usernameFromUrl, SupportedCredentialTypes types) =>
							new UsernamePasswordCredentials
							{
								Username = docsSettings!.GitLogin?.Username ?? throw new BuildException("GitLogin has a null Username."),
								Password = docsSettings!.GitLogin!.Password ?? throw new BuildException("GitLogin has a null Password."),
							};
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
						dotnetFormat.Run("--verbosity", GetVerbosity());
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
					yield return $"--properties:Configuration={configurationOption!.Value}";

					var platformValue = platformOption!.Value ?? settings!.SolutionPlatform;
					yield return platformValue is null ? null : $"--properties:Platform={platformValue}";
				}
			}

			string? GetPlatformArg()
			{
				var platformValue = platformOption!.Value ?? settings!.SolutionPlatform;
				return platformValue is null ? null : $"-p:Platform={platformValue}";
			}

			string? GetMaxCpuCountArg() =>
				settings!.MaxCpuCount is not null ? $"-maxcpucount:{settings.MaxCpuCount}" :
				msbuildSettings is not null ? "-maxcpucount" : null;

			string? GetBuildNumberArg()
			{
				var buildNumberValue = buildNumberOption!.Value ??
					Environment.GetEnvironmentVariable("APPVEYOR_BUILD_NUMBER") ??
					Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER");
				return buildNumberValue is null ? null : $"-p:BuildNumber={buildNumberValue}";
			}

			IEnumerable<string> GetExtraProperties(string target)
			{
				var pairs = settings!.ExtraProperties?.Invoke(target!);
				if (pairs is not null)
				{
					foreach (var (key, value) in pairs)
						yield return $"-p:{key}={value}";
				}
			}

			void MSBuild(IEnumerable<string?> arguments) => RunMSBuild(msbuildSettings, arguments!);

			string GetVerbosity()
			{
				var verbosity = verbosityOption!.Value?.ToLowerInvariant() switch
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
					_ => throw new BuildException($"Unexpected verbosity option: {verbosityOption.Value}"),
				};

				return verbosity switch
				{
					DotNetBuildVerbosity.Quiet => "quiet",
					DotNetBuildVerbosity.Minimal => "minimal",
					DotNetBuildVerbosity.Normal => "normal",
					DotNetBuildVerbosity.Detailed => "detailed",
					DotNetBuildVerbosity.Diagnostic => "diagnostic",
					_ => throw new BuildException($"Unexpected DotNetBuildVerbosity: {settings!.Verbosity}"),
				};
			}
		}

		private static (string Name, string Version, string Suffix) GetPackageInfo(string path)
		{
			var match = Regex.Match(path, @"[/\\](?<name>[^/\\]+)\.(?<version>[0-9]+\.[0-9]+\.[0-9]+(-(?<suffix>.+))?)\.nupkg$", RegexOptions.ExplicitCapture);
			return (match.Groups["name"].Value, match.Groups["version"].Value, match.Groups["suffix"].Value);
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
				.ThenByDescending(x => x.Version.Suffix is null)
				.ThenByDescending(x => x.Version.Suffix, StringComparer.Ordinal)
				.Select(x => x.Tag)
				.Concat(tags.Where(x => x.StartsWith("publish-", StringComparison.Ordinal)))
				.FirstOrDefault();
	}
}
