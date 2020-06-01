using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
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
			var verbosity = GetVerbosity(settings.Verbosity);

			var dotNetTools = settings.DotNetTools ?? new DotNetTools(Path.Combine("tools", "bin"));
			var xmlDocMarkdownVersion = settings.DocsSettings?.ToolVersion ?? "2.0.1";

			var packagePaths = new List<string>();
			string? trigger = null;
			var ignoreIfAlreadyPushed = false;

			build.Target("clean")
				.Describe("Deletes all build output")
				.Does(() =>
				{
					var findDirectoriesToDelete = settings.CleanSettings?.FindDirectoriesToDelete ?? (() => FindDirectories("{src,tests}/**/{bin,obj}", "tools/XmlDocTarget/{bin,obj}"));
					foreach (var directoryToDelete in findDirectoriesToDelete())
						deleteDirectory(directoryToDelete);

					var extraProperties = getExtraProperties("clean");
					if (msbuildSettings == null)
						RunDotNet(new[] { "clean", solutionName, "-c", configurationOption.Value, getPlatformArg(), "--verbosity", verbosity, getMaxCpuCountArg() }.Concat(extraProperties));
					else
						runMSBuild(new[] { solutionName, "-t:Clean", $"-p:Configuration={configurationOption.Value}", getPlatformArg(), $"-v:{verbosity}", getMaxCpuCountArg() }.Concat(extraProperties));
				});

			build.Target("restore")
				.Describe("Restores NuGet packages")
				.Does(() =>
				{
					var extraProperties = getExtraProperties("restore");
					if (msbuildSettings == null)
						RunDotNet(new[] { "restore", solutionName, getPlatformArg(), "--verbosity", verbosity, getMaxCpuCountArg() }.Concat(extraProperties));
					else
						runMSBuild(new[] { solutionName, "-t:Restore", $"-p:Configuration={configurationOption.Value}", getPlatformArg(), $"-v:{verbosity}", getMaxCpuCountArg() }.Concat(extraProperties));
				});

			build.Target("build")
				.DependsOn("restore")
				.Describe("Builds the solution")
				.Does(() =>
				{
					var buildNumberArg = buildNumberOption.Value == null ? null : $"-p:BuildNumber={buildNumberOption.Value}";

					var extraProperties = getExtraProperties("build");
					if (msbuildSettings == null)
						RunDotNet(new[] { "build", solutionName, "-c", configurationOption.Value, getPlatformArg(), buildNumberArg, "--no-restore", "--verbosity", verbosity, getMaxCpuCountArg() }.Concat(extraProperties));
					else
						runMSBuild(new[] { solutionName, $"-p:Configuration={configurationOption.Value}", getPlatformArg(), buildNumberArg, $"-v:{verbosity}", getMaxCpuCountArg() }.Concat(extraProperties));
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
						var extraProperties = getExtraProperties("test").ToList();
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
									RunDotNet(new[] { "test", testProject, "-c", configurationOption.Value, getPlatformArg(), "--no-build", getMaxCpuCountArg() }.Concat(extraProperties));
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

					var extraProperties = getExtraProperties("package").ToList();
					foreach (var packageProject in packageProjects)
					{
						if (msbuildSettings == null)
						{
							RunDotNet(new[]
							{
								"pack", packageProject,
								"-c", configurationOption.Value,
								getPlatformArg(),
								"--no-build",
								"--output", tempOutputPath,
								versionSuffix != null ? "--version-suffix" : null, versionSuffix,
								getMaxCpuCountArg()
							}.Concat(extraProperties));
						}
						else
						{
							runMSBuild(new[]
							{
								packageProject, "-t:Pack",
								$"-p:Configuration={configurationOption.Value}",
								getPlatformArg(),
								"-p:NoBuild=true",
								$"-p:PackageOutputPath={tempOutputPath}",
								versionSuffix != null ? $"-p:VersionSuffix={versionSuffix}" : null,
								$"-v:{verbosity}",
								getMaxCpuCountArg()
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
					deleteDirectory(tempOutputPath);

					if (packagePaths.Count == 0)
						throw new ApplicationException("No NuGet packages created.");
				});

			build.Target("publish")
				.Describe("Publishes NuGet packages and documentation")
				.DependsOn("package")
				.Does(() =>
				{
					if (packagePaths.Count == 0)
						throw new ApplicationException("No NuGet packages found.");

					if (trigger == null)
						throw new ApplicationException("--trigger option required.");

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
							throw new ApplicationException($"Trigger '{trigger}' doesn't match package version: {string.Join(", ", mismatches.Select(Path.GetFileName))}");

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

						Credentials provideCredentials(string url, string usernameFromUrl, SupportedCredentialTypes types) =>
							new UsernamePasswordCredentials
							{
								Username = docsSettings?.GitLogin?.Username ?? throw new ApplicationException("GitLogin has a null Username."),
								Password = docsSettings?.GitLogin?.Password ?? throw new ApplicationException("GitLogin has a null Password."),
							};

						if (shouldPublishDocs && docsSettings != null)
						{
							if (docsSettings.GitLogin == null || docsSettings.GitAuthor == null)
								throw new ApplicationException("GitLogin and GitAuthor must be set on DocsSettings.");

							var gitRepositoryUrl = docsSettings.GitRepositoryUrl;
							gitBranchName = docsSettings.GitBranchName;

							if (gitRepositoryUrl != null)
							{
								cloneDirectory = "docs_repo_" + Path.GetRandomFileName();
								Repository.Clone(sourceUrl: gitRepositoryUrl, workdirPath: cloneDirectory,
									options: new CloneOptions { BranchName = gitBranchName, CredentialsProvider = provideCredentials });
								repoDirectory = cloneDirectory;
							}
							else
							{
								repoDirectory = ".";
							}

							using var repository = new Repository(repoDirectory);
							if (gitRepositoryUrl != null)
							{
								if (gitBranchName == null)
									gitBranchName = repository.Head.FriendlyName;
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
									if (autoBranchName != null)
										branch = repository.Branches[autoBranchName] ?? repository.CreateBranch(autoBranchName);
									else
										branch = repository.Branches.FirstOrDefault(x => x.Tip.Sha == repository.Head.Tip.Sha);
									if (branch != null)
										Commands.Checkout(repository, branch);
								}
								if (branch == null)
									throw new ArgumentException("Could not determine repository branch for publishing docs.");
								gitBranchName = branch.FriendlyName;
							}

							var projectHasDocs = docsSettings.ProjectHasDocs ?? (_ => true);
							foreach (var projectName in packagePaths.Select(x => GetPackageInfo(x).Name).Where(projectHasDocs))
							{
								string findAssembly(string name) =>
									FindFiles($"tools/XmlDocTarget/bin/**/{name}.dll").OrderByDescending(File.GetLastWriteTime).FirstOrDefault() ??
									FindFiles($"src/{name}/bin/**/{name}.dll").OrderByDescending(File.GetLastWriteTime).FirstOrDefault();

								var assemblyPaths = new List<string>();
								if (docsSettings.FindAssemblies != null)
								{
									assemblyPaths.AddRange(docsSettings.FindAssemblies(projectName));
								}
								else
								{
									var assemblyPath = (docsSettings.FindAssembly ?? findAssembly)(projectName);
									if (assemblyPath != null)
										assemblyPaths.Add(assemblyPath);
								}

								if (assemblyPaths.Count != 0)
								{
									foreach (var assemblyPath in assemblyPaths)
									{
										RunApp(dotNetTools.GetToolPath($"xmldocmd/{xmlDocMarkdownVersion}"), assemblyPath,
											Path.Combine(repoDirectory, docsSettings.TargetDirectory ?? "docs"),
											"--source", $"{docsSettings.SourceCodeUrl}/{projectName}", "--newline", "lf", "--clean");
									}
								}
								else
								{
									Console.WriteLine($"Documentation not generated for {projectName}; assembly not found.");
								}
							}

							shouldPushDocs = repository.RetrieveStatus().IsDirty;
						}

						if (shouldPublishPackages)
						{
							var nugetApiKey = settings.NuGetApiKey;
							if (string.IsNullOrEmpty(nugetApiKey))
								throw new ApplicationException("NuGetApiKey required to publish.");

							if (ignoreIfAlreadyPushed)
							{
								var nugetSettings = NuGet.Configuration.Settings.LoadDefaultSettings(root: null);
								var packageSourceProvider = new PackageSourceProvider(nugetSettings);
								var sourceRepositoryProvider = new SourceRepositoryProvider(packageSourceProvider, NuGet.Protocol.Core.Types.Repository.Provider.GetCoreV3());
								using var sourceCacheContext = new SourceCacheContext();
								var nugetRepositories = sourceRepositoryProvider.GetRepositories()
									.Select(x => x.GetResourceAsync<DependencyInfoResource>().GetAwaiter().GetResult())
									.ToList();

								foreach (var packagePath in packagePaths.ToList())
								{
									var packageInfo = GetPackageInfo(packagePath);
									var package = new PackageIdentity(packageInfo.Name, NuGetVersion.Parse(packageInfo.Version));

									foreach (var nugetRepository in nugetRepositories)
									{
										var dependencyInfo = nugetRepository.ResolvePackage(package, NuGetFramework.AnyFramework,
											sourceCacheContext, NullLogger.Instance, CancellationToken.None).GetAwaiter().GetResult();
										if (dependencyInfo != null)
										{
											Console.WriteLine($"Package already pushed: {packageInfo.Name} {packageInfo.Version}");
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
								$"refs/heads/{gitBranchName}", new PushOptions { CredentialsProvider = provideCredentials });
						}

						if (cloneDirectory != null)
						{
							// delete the cloned directory
							foreach (var fileInfo in FindFiles(cloneDirectory, "**").Select(x => new FileInfo(x)).Where(x => x.IsReadOnly))
								fileInfo.IsReadOnly = false;
							deleteDirectory(cloneDirectory);
						}
					}
					else
					{
						Console.WriteLine("To publish to NuGet, push this tag: v" + GetPackageInfo(packagePaths[0]).Version);
					}
				});

			string? getPlatformArg()
			{
				var platformValue = platformOption?.Value ?? settings?.SolutionPlatform;
				return platformValue == null ? null : $"-p:Platform={platformValue}";
			}

			string? getMaxCpuCountArg()
			{
				if (settings!.MaxCpuCount != null)
					return $"-maxcpucount:{settings.MaxCpuCount}";
				else if (msbuildSettings != null)
					return "-maxcpucount";
				else
					return null;
			}

			IEnumerable<string> getExtraProperties(string target)
			{
				var pairs = settings!.ExtraProperties?.Invoke(target);
				if (pairs != null)
				{
					foreach (var pair in pairs)
						yield return $"-p:{pair.Key}={pair.Value}";
				}
			}

			void runMSBuild(IEnumerable<string?> arguments) => RunMSBuild(msbuildSettings, arguments);

			void deleteDirectory(string path)
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

		private static string GetVerbosity(DotNetBuildVerbosity? verbosity) =>
			verbosity switch
			{
				DotNetBuildVerbosity.Quiet => "quiet",
				DotNetBuildVerbosity.Minimal => "minimal",
				DotNetBuildVerbosity.Detailed => "detailed",
				DotNetBuildVerbosity.Diagnostic => "diagnostic",
				_ => "normal",
			};
	}
}
