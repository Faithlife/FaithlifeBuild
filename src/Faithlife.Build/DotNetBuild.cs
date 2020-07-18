using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
using Polly;
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

			var buildOptions = settings.BuildOptions ?? (settings.BuildOptions = new DotNetBuildOptions());
			var configurationOption = buildOptions.ConfigurationOption ?? (buildOptions.ConfigurationOption =
				build.AddOption("-c|--configuration <name>", "The configuration to build (default Release)", "Release"));
			var platformOption = buildOptions.PlatformOption ?? (buildOptions.PlatformOption =
				build.AddOption("-p|--platform <name>", "The solution platform to build"));
			var verbosityOption = buildOptions.VerbosityOption ?? (buildOptions.VerbosityOption =
				build.AddOption("-v|--verbosity <level>", "The build verbosity (q[uiet], m[inimal], n[ormal], d[etailed])"));
			var versionSuffixOption = buildOptions.VersionSuffixOption ?? (buildOptions.VersionSuffixOption =
				build.AddOption("--version-suffix <suffix>", "Generates a prerelease package"));
			var nugetOutputOption = buildOptions.NuGetOutputOption ?? (buildOptions.NuGetOutputOption =
				build.AddOption("--nuget-output <path>", "Directory for generated package (default release)", "release"));
			var triggerOption = buildOptions.TriggerOption ?? (buildOptions.TriggerOption =
				build.AddOption("--trigger <name>", "The git branch or tag that triggered the build"));
			var buildNumberOption = buildOptions.BuildNumberOption ?? (buildOptions.BuildNumberOption =
				build.AddOption("--build-number <number>", "The automated build number"));
			var noTestFlag = buildOptions.NoTestFlag ?? (buildOptions.NoTestFlag =
				build.AddFlag("--no-test", "Skip the unit tests"));

			var solutionName = settings.SolutionName;
			var nugetSource = settings.NuGetSource ?? "https://api.nuget.org/v3/index.json";
			var msbuildSettings = settings.MSBuildSettings;
			var verbosity = GetVerbosity(verbosityOption.Value, settings.Verbosity);
			var localDotNetToolVersions = GetLocalDotNetToolVersions();

			var packagePaths = new List<string>();
			string? trigger = null;
			var ignoreIfAlreadyPushed = false;

			build.Target("clean")
				.Describe("Deletes all build output")
				.Does(() =>
				{
					var findDirectoriesToDelete = settings.CleanSettings?.FindDirectoriesToDelete ??
						(() => FindDirectories("{src,tests,tools}/**/{bin,obj}").Except(FindDirectories("tools/bin")).ToList());
					foreach (var directoryToDelete in findDirectoriesToDelete())
						DeleteDirectory(directoryToDelete);

					var extraProperties = GetExtraProperties("clean");
					if (msbuildSettings == null)
						RunDotNet(new[] { "clean", solutionName, "-c", configurationOption.Value, GetPlatformArg(), "--verbosity", verbosity, GetMaxCpuCountArg() }.Concat(extraProperties));
					else
						RunMsBuild(new[] { solutionName, "-t:Clean", $"-p:Configuration={configurationOption.Value}", GetPlatformArg(), $"-v:{verbosity}", GetMaxCpuCountArg() }.Concat(extraProperties));
				});

			build.Target("restore")
				.Describe("Restores NuGet packages")
				.Does(() =>
				{
					var extraProperties = GetExtraProperties("restore");
					if (msbuildSettings == null)
						RunDotNet(new[] { "restore", solutionName, GetPlatformArg(), "--verbosity", verbosity, GetMaxCpuCountArg() }.Concat(extraProperties));
					else
						RunMsBuild(new[] { solutionName, "-t:Restore", $"-p:Configuration={configurationOption.Value}", GetPlatformArg(), $"-v:{verbosity}", GetMaxCpuCountArg() }.Concat(extraProperties));

					if (localDotNetToolVersions.Count != 0)
						RunDotNet("tool", "restore");
				});

			build.Target("build")
				.DependsOn("restore")
				.Describe("Builds the solution")
				.Does(() =>
				{
					var buildNumberArg = GetBuildNumberArg();

					var extraProperties = GetExtraProperties("build");
					if (msbuildSettings == null)
						RunDotNet(new[] { "build", solutionName, "-c", configurationOption.Value, GetPlatformArg(), buildNumberArg, "--no-restore", "--verbosity", verbosity, GetMaxCpuCountArg() }.Concat(extraProperties));
					else
						RunMsBuild(new[] { solutionName, $"-p:Configuration={configurationOption.Value}", GetPlatformArg(), buildNumberArg, $"-v:{verbosity}", GetMaxCpuCountArg() }.Concat(extraProperties));
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
						if (findTestAssemblies != null)
						{
							foreach (var testAssembly in findTestAssemblies())
							{
								if (settings.TestSettings?.RunTests != null)
									settings.TestSettings.RunTests(testAssembly);
								else
									RunDotNet(new AppRunnerSettings { Arguments = new[] { "vstest", Path.GetFileName(testAssembly) }.Concat(extraProperties), WorkingDirectory = Path.GetDirectoryName(testAssembly) });
							}
						}
						else
						{
							var testProjects = new List<string?>();

							var findTestProjects = settings.TestSettings?.FindProjects;
							if (findTestProjects != null)
								testProjects.AddRange(findTestProjects());
							else
								testProjects.Add(solutionName);

							foreach (var testProject in testProjects)
							{
								if (settings.TestSettings?.RunTests != null)
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
					trigger = triggerOption.Value;

					if (trigger == "detect")
					{
						using var repository = new Repository(".");
						var headSha = repository.Head.Tip.Sha;
						var autoTrigger = GetBestTriggerFromTags(repository.Tags.Where(x => x.Target.Sha == headSha).Select(x => x.FriendlyName).ToList());
						if (autoTrigger != null)
						{
							trigger = autoTrigger;
							ignoreIfAlreadyPushed = true;
							Console.WriteLine($"Detected trigger: {trigger}");
						}
					}

					var versionSuffix = versionSuffixOption.Value;
					if (versionSuffix == null && trigger != null)
						versionSuffix = GetVersionFromTrigger(trigger) is string triggerVersion ? SplitVersion(triggerVersion).Suffix : null;

					var nugetOutputPath = Path.GetFullPath(nugetOutputOption.Value);
					var tempOutputPath = Path.Combine(nugetOutputPath, $"temp_{Guid.NewGuid():N}");

					var packageProjects = new List<string?>();

					var findPackageProjects = settings.PackageSettings?.FindProjects;
					if (findPackageProjects != null)
						packageProjects.AddRange(findPackageProjects());
					else
						packageProjects.Add(solutionName);

					var extraProperties = GetExtraProperties("package").ToList();
					foreach (var packageProject in packageProjects)
					{
						if (msbuildSettings == null)
						{
							RunDotNet(new[]
							{
								"pack", packageProject,
								"-c", configurationOption.Value,
								GetPlatformArg(),
								"--no-build",
								"--output", tempOutputPath,
								versionSuffix != null ? "--version-suffix" : null, versionSuffix,
								GetMaxCpuCountArg(),
							}.Concat(extraProperties));
						}
						else
						{
							RunMsBuild(new[]
							{
								packageProject, "-t:Pack",
								$"-p:Configuration={configurationOption.Value}",
								GetPlatformArg(),
								"-p:NoBuild=true",
								$"-p:PackageOutputPath={tempOutputPath}",
								versionSuffix != null ? $"-p:VersionSuffix={versionSuffix}" : null,
								$"-v:{verbosity}",
								GetMaxCpuCountArg(),
							}.Concat(extraProperties));
						}
					}

					var tempPackagePaths = FindFilesFrom(tempOutputPath, "*.nupkg");
					foreach (var tempPackagePath in tempPackagePaths)
					{
						var packagePath = Path.Combine(nugetOutputPath, Path.GetFileName(tempPackagePath) ?? throw new InvalidOperationException());
						if (File.Exists(packagePath))
							File.Delete(packagePath);
						File.Move(tempPackagePath, packagePath);
						packagePaths.Add(packagePath);
					}
					DeleteDirectory(tempOutputPath);

					if (packagePaths.Count == 0)
						throw new BuildException("No NuGet packages created.");
				});

			build.Target("publish")
				.Describe("Publishes NuGet packages and documentation")
				.DependsOn("package")
				.Does(() =>
				{
					if (packagePaths.Count == 0)
						throw new BuildException("No NuGet packages found.");

					trigger ??= "publish-all";

					var triggerParts = trigger.Split('-');
					var publishTrigger = triggerParts.Length >= 2 && triggerParts[0] == "publish" ? triggerParts[1] : null;
					var shouldPublishPackages = publishTrigger == "package" || publishTrigger == "packages" || publishTrigger == "all";
					var shouldPublishDocs = publishTrigger == "docs" || publishTrigger == "all";
					var shouldSkipDuplicates = publishTrigger == "all";

					var triggerVersion = GetVersionFromTrigger(trigger);
					if (triggerVersion != null)
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

						if (shouldPublishDocs && docsSettings != null)
						{
							if (docsSettings.GitLogin == null || docsSettings.GitAuthor == null)
								throw new BuildException("GitLogin and GitAuthor must be set on DocsSettings.");

							var gitRepositoryUrl = docsSettings.GitRepositoryUrl;
							gitBranchName = docsSettings.GitBranchName;

							if (gitRepositoryUrl != null)
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
							if (gitRepositoryUrl != null)
							{
								gitBranchName ??= repository.Head.FriendlyName;
							}
							else if (gitBranchName != null)
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
								if (branch == null)
								{
									var autoBranchName = Environment.GetEnvironmentVariable("APPVEYOR_REPO_BRANCH");

									if (autoBranchName == null)
									{
										var gitRef = Environment.GetEnvironmentVariable("GITHUB_REF");
										const string prefix = "refs/heads/";
										if (gitRef.StartsWith(prefix, StringComparison.Ordinal))
											autoBranchName = gitRef.Substring(prefix.Length);
									}

									if (autoBranchName != null)
										branch = repository.Branches[autoBranchName] ?? repository.CreateBranch(autoBranchName);
									else
										branch = repository.Branches.FirstOrDefault(x => x.Tip.Sha == repository.Head.Tip.Sha);

									if (branch != null)
										Commands.Checkout(repository, branch);
								}
								if (branch == null)
									throw new BuildException("Could not determine repository branch for publishing docs.");
								gitBranchName = branch.FriendlyName;
							}

							var projectHasDocs = docsSettings.ProjectHasDocs ?? (_ => true);
							foreach (var projectName in packagePaths.Select(x => GetPackageInfo(x).Name).Where(projectHasDocs))
							{
								var assemblyPaths = new List<string>();
								if (docsSettings.FindAssemblies != null)
								{
									assemblyPaths.AddRange(docsSettings.FindAssemblies(projectName));
								}
								else
								{
									var assemblyPath = (docsSettings.FindAssembly ?? FindAssembly)(projectName);
									if (assemblyPath != null)
										assemblyPaths.Add(assemblyPath);
								}

								if (assemblyPaths.Count != 0)
								{
									if (localDotNetToolVersions.ContainsKey("xmldocmd"))
									{
										foreach (var assemblyPath in assemblyPaths)
										{
											RunDotNetTool("xmldocmd", assemblyPath,
												Path.Combine(repoDirectory, docsSettings.TargetDirectory ?? "docs"),
												"--source", $"{docsSettings.SourceCodeUrl}/{projectName}", "--newline", "lf", "--clean");
										}
									}
									else
									{
										var dotNetTools = settings.DotNetTools ?? new DotNetTools(Path.Combine("tools", "bin"));
										var xmlDocMarkdownVersion = settings.DocsSettings?.ToolVersion ?? "2.0.1";

										foreach (var assemblyPath in assemblyPaths)
										{
											RunApp(dotNetTools.GetToolPath($"xmldocmd/{xmlDocMarkdownVersion}"), assemblyPath,
												Path.Combine(repoDirectory, docsSettings.TargetDirectory ?? "docs"),
												"--source", $"{docsSettings.SourceCodeUrl}/{projectName}", "--newline", "lf", "--clean");
										}
									}
								}
								else
								{
									Console.WriteLine($"Documentation not generated for {projectName}; assembly not found.");
								}

								static string FindAssembly(string name) =>
									FindFiles($"tools/XmlDocTarget/bin/**/{name}.dll").OrderByDescending(File.GetLastWriteTime).FirstOrDefault() ??
									FindFiles($"src/{name}/bin/**/{name}.dll").OrderByDescending(File.GetLastWriteTime).FirstOrDefault();
							}

							shouldPushDocs = repository.RetrieveStatus().IsDirty;
						}

						if (shouldPublishPackages)
						{
							var nugetApiKey = settings.NuGetApiKey;
							if (string.IsNullOrEmpty(nugetApiKey))
								throw new BuildException("NuGetApiKey required to publish.");

							if (ignoreIfAlreadyPushed)
							{
								var nugetSettings = Settings.LoadDefaultSettings(root: null);
								var packageSourceProvider = new PackageSourceProvider(nugetSettings);
								var sourceRepositoryProvider = new SourceRepositoryProvider(packageSourceProvider, NuGet.Protocol.Core.Types.Repository.Provider.GetCoreV3());
								using var sourceCacheContext = new SourceCacheContext();
								var nugetRepositories = sourceRepositoryProvider.GetRepositories()
									.Select(x => x.GetResourceAsync<DependencyInfoResource>().GetAwaiter().GetResult())
									.ToList();

								foreach (var packagePath in packagePaths.ToList())
								{
									var (packageName, packageVersion, _) = GetPackageInfo(packagePath);
									var package = new PackageIdentity(packageName, NuGetVersion.Parse(packageVersion));

									foreach (var nugetRepository in nugetRepositories)
									{
										var dependencyInfo = nugetRepository.ResolvePackage(package, NuGetFramework.AnyFramework,
											sourceCacheContext, NullLogger.Instance, CancellationToken.None).GetAwaiter().GetResult();
										if (dependencyInfo != null)
										{
											Console.WriteLine($"Package already pushed: {packageName} {packageVersion}");
											packagePaths.Remove(packagePath);
											break;
										}
									}
								}
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

						if (cloneDirectory != null)
						{
							// delete the cloned directory
							foreach (var fileInfo in FindFiles(cloneDirectory, "**").Select(x => new FileInfo(x)).Where(x => x.IsReadOnly))
								fileInfo.IsReadOnly = false;
							DeleteDirectory(cloneDirectory);
						}

						Credentials ProvideCredentials(string url, string usernameFromUrl, SupportedCredentialTypes types) =>
							new UsernamePasswordCredentials
							{
								Username = docsSettings?.GitLogin?.Username ?? throw new BuildException("GitLogin has a null Username."),
								Password = docsSettings?.GitLogin?.Password ?? throw new BuildException("GitLogin has a null Password."),
							};
					}
					else
					{
						Console.WriteLine("To publish to NuGet, push this tag: v" + GetPackageInfo(packagePaths[0]).Version);
					}
				});

			if (localDotNetToolVersions.ContainsKey("dotnet-format"))
			{
				build.Target("format")
					.DependsOn("restore")
					.Describe("Fixes coding style with dotnet-format")
					.Does(() =>
					{
						RunDotNet("dotnet-format", "--verbosity", verbosity);
					});
			}

			if (localDotNetToolVersions.ContainsKey("jetbrains.resharper.globaltools"))
			{
				build.Target("cleanup")
					.DependsOn("restore")
					.Describe("Fixes coding style with JetBrains CleanupCode")
					.Does(() =>
					{
						RunDotNet("jb", "cleanupcode",
							"--profile=Build",
							"--verbosity=ERROR",
							"--disable-settings-layers:GlobalAll;GlobalPerProduct;SolutionPersonal;ProjectPersonal",
							GetSolutionName());
					});

				build.Target("inspect")
					.DependsOn("restore")
					.Describe("Checks coding style with JetBrains InspectCode")
					.Does(() =>
					{
						var outputPath = Path.Combine("release", "inspect.xml");

						RunDotNet("jb", "inspectcode",
							"--severity=WARNING",
							"--verbosity=ERROR",
							"--format=Xml",
							"--disable-settings-layers:GlobalAll;GlobalPerProduct;SolutionPersonal;ProjectPersonal",
							$"--output={outputPath}",
							GetSolutionName());

						var outputDocument = XDocument.Load(outputPath);
						var issueElements = outputDocument.XPathSelectElements("//Issue").ToList();
						foreach (var issueElement in issueElements)
							Console.WriteLine($"{issueElement.Attribute("File")!.Value}({issueElement.Attribute("Line")!.Value}): {issueElement.Attribute("Message")!.Value}");
						if (issueElements.Count != 0)
							throw new BuildException($"{issueElements.Count} inspection issues found.");
					});

				string GetSolutionName()
				{
					if (solutionName != null)
						return solutionName;

					var solutionNames = FindFiles("*.sln");
					if (solutionNames.Count == 0)
						throw new BuildException("Solution file not found.");
					if (solutionNames.Count != 0)
						throw new BuildException("Multiple solution files found.");
					return solutionNames[0];
				}
			}

			string? GetPlatformArg()
			{
				var platformValue = platformOption?.Value ?? settings?.SolutionPlatform;
				return platformValue == null ? null : $"-p:Platform={platformValue}";
			}

			string? GetMaxCpuCountArg() =>
				settings!.MaxCpuCount != null ? $"-maxcpucount:{settings.MaxCpuCount}" :
				msbuildSettings != null ? "-maxcpucount" : null;

			string? GetBuildNumberArg()
			{
				var buildNumberValue = buildNumberOption!.Value ??
					Environment.GetEnvironmentVariable("APPVEYOR_BUILD_NUMBER") ??
					Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER");
				return buildNumberValue == null ? null : $"-p:BuildNumber={buildNumberValue}";
			}

			IEnumerable<string> GetExtraProperties(string target)
			{
				var pairs = settings!.ExtraProperties?.Invoke(target);
				if (pairs != null)
				{
					foreach (var (key, value) in pairs)
						yield return $"-p:{key}={value}";
				}
			}

			void RunMsBuild(IEnumerable<string?> arguments) => RunMSBuild(msbuildSettings, arguments);

			void DeleteDirectory(string path)
			{
				Policy.Handle<IOException>()
					.WaitAndRetry(new[] { TimeSpan.FromMilliseconds(50) })
					.Execute(() =>
					{
						try
						{
							Directory.Delete(path, recursive: true);
						}
						catch (DirectoryNotFoundException)
						{
						}
					});
			}

			IReadOnlyDictionary<string, string> GetLocalDotNetToolVersions()
			{
				var manifestPaths = new[] { "dotnet-tools.json", Path.Combine(".config", "dotnet-tools.json") }.Where(File.Exists).ToList();
				if (manifestPaths.Count > 1)
					throw new BuildException($"Multiple .NET local tool manifests: {string.Join(", ", manifestPaths)}");
				if (manifestPaths.Count == 0)
					return new Dictionary<string, string>();

				return JsonDocument.Parse(File.ReadAllText(manifestPaths[0])).RootElement
					.GetProperty("tools")
					.EnumerateObject()
					.ToDictionary(x => x.Name, x => x.Value.GetProperty("version").GetString());
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
			return (int.Parse(dotParts[0]), int.Parse(dotParts[1]), int.Parse(dotParts[2]), hyphenParts.ElementAtOrDefault(1));
		}

		private static string GetBestTriggerFromTags(IReadOnlyList<string> tags) =>
			tags
				.Select(x => (Tag: x, Version: GetVersionFromTrigger(x)))
				.Where(x => x.Version != null)
				.Select(x => (x.Tag, Version: SplitVersion(x.Version!)))
				.OrderByDescending(x => x.Version.Major)
				.ThenByDescending(x => x.Version.Minor)
				.ThenByDescending(x => x.Version.Patch)
				.ThenByDescending(x => x.Version.Suffix == null)
				.ThenByDescending(x => x.Version.Suffix, StringComparer.Ordinal)
				.Select(x => x.Tag)
				.Concat(tags.Where(x => x.StartsWith("publish-", StringComparison.Ordinal)))
				.FirstOrDefault();

		private static string GetVerbosity(string? verbosityOptionValue, DotNetBuildVerbosity? verbositySetting)
		{
			var verbosity = verbosityOptionValue?.ToLowerInvariant() switch
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
				null => verbositySetting ?? DotNetBuildVerbosity.Minimal,
				_ => throw new BuildException($"Unexpected verbosity option: {verbosityOptionValue}"),
			};

			return verbosity switch
			{
				DotNetBuildVerbosity.Quiet => "quiet",
				DotNetBuildVerbosity.Minimal => "minimal",
				DotNetBuildVerbosity.Normal => "normal",
				DotNetBuildVerbosity.Detailed => "detailed",
				DotNetBuildVerbosity.Diagnostic => "diagnostic",
				_ => throw new BuildException($"Unexpected DotNetBuildVerbosity: {verbositySetting}"),
			};
		}
	}
}
