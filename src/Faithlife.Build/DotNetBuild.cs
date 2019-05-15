using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LibGit2Sharp;
using static Faithlife.Build.AppRunner;
using static Faithlife.Build.BuildUtility;
using static Faithlife.Build.DotNetRunner;
using static Faithlife.Build.MSBuildRunner;

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
		public static void AddDotNetTargets(this BuildApp build, DotNetBuildSettings settings = null)
		{
			settings = settings ?? new DotNetBuildSettings();

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

			var solutionName = settings.SolutionName;
			var nugetSource = settings.NuGetSource ?? "https://api.nuget.org/v3/index.json";
			var msbuildSettings = settings.MSBuildSettings;

			var dotNetTools = settings.DotNetTools ?? new DotNetTools(Path.Combine("tools", "bin"));
			var sourceLinkVersion = settings.SourceLinkToolVersion ?? "3.0.0";
			var xmlDocMarkdownVersion = settings.DocsSettings?.ToolVersion ?? "1.5.1";

			build.Target("clean")
				.Describe("Deletes all build output")
				.Does(() =>
				{
					foreach (var directory in FindDirectories("{src,tests}/**/{bin,obj}", "tools/bin", "release"))
						Directory.Delete(directory, recursive: true);
				});

			build.Target("restore")
				.Describe("Restores NuGet packages")
				.Does(() =>
				{
					if (msbuildSettings == null)
						RunDotNet("restore", solutionName, "--verbosity", "normal");
					else
						runMSBuild(solutionName, "-t:Restore", $"-p:Configuration={configurationOption.Value}", getPlatformArg());
				});

			build.Target("build")
				.DependsOn("restore")
				.Describe("Builds the solution")
				.Does(() =>
				{
					if (msbuildSettings == null)
						RunDotNet("build", solutionName, "-c", configurationOption.Value, getPlatformArg(), "--no-restore", "--verbosity", "normal");
					else
						runMSBuild(solutionName, $"-p:Configuration={configurationOption.Value}", getPlatformArg());
				});

			build.Target("test")
				.DependsOn("build")
				.Describe("Runs the unit tests")
				.Does(() =>
				{
					var findTestAssemblies = settings.TestSettings?.FindTestAssemblies;
					if (findTestAssemblies != null)
					{
						foreach (var testAssembly in findTestAssemblies())
							RunDotNet("vstest", testAssembly);
					}
					else
					{
						RunDotNet("test", solutionName, "-c", configurationOption.Value, getPlatformArg(), "--no-build");
					}
				});

			build.Target("package")
				.DependsOn("test")
				.Describe("Builds NuGet packages")
				.Does(() =>
				{
					string versionSuffix = versionSuffixOption.Value;
					string trigger = triggerOption.Value;
					if (versionSuffix == null && trigger != null)
					{
						var group = s_triggerRegex.Match(trigger).Groups["suffix"];
						if (group.Success)
							versionSuffix = group.ToString();
					}

					if (msbuildSettings == null)
					{
						RunDotNet("pack", solutionName,
							"-c", configurationOption.Value,
							getPlatformArg(),
							"--no-build",
							"--output", Path.GetFullPath(nugetOutputOption.Value),
							versionSuffix != null ? "--version-suffix" : null, versionSuffix);
					}
					else
					{
						runMSBuild(solutionName, "-t:Pack",
							$"-p:Configuration={configurationOption.Value}",
							getPlatformArg(),
							"-p:NoBuild=true",
							$"-p:PackageOutputPath={Path.GetFullPath(nugetOutputOption.Value)}",
							versionSuffix != null ? $"-p:VersionSuffix={versionSuffix}" : null);
					}
				});

			build.Target("publish")
				.Describe("Publishes NuGet packages and documentation")
				.DependsOn("clean", "package")
				.Does(() =>
				{
					var packagePaths = FindFilesFrom(Path.GetFullPath(nugetOutputOption.Value), "*.nupkg");
					if (packagePaths.Count == 0)
						throw new ApplicationException("No NuGet packages found.");

					var trigger = triggerOption.Value;
					if (trigger == null)
						throw new ApplicationException("--trigger option required.");

					bool shouldPublishPackages = trigger == "publish-package" || trigger == "publish-packages" || trigger == "publish-all";
					bool shouldPublishDocs = trigger == "publish-docs" || trigger == "publish-all";

					var triggerMatch = s_triggerRegex.Match(trigger);
					if (triggerMatch.Success)
					{
						var triggerName = triggerMatch.Groups["name"].Value;
						var triggerVersion = triggerMatch.Groups["version"].Value;
						if (triggerName.Length == 0)
						{
							var mismatches = packagePaths.Where(x => GetPackageInfo(x).Version != triggerVersion).ToList();
							if (mismatches.Count != 0)
								throw new ApplicationException($"Trigger '{trigger}' doesn't match package version: {string.Join(", ", mismatches.Select(Path.GetFileName))}");
						}
						else
						{
							var matches = packagePaths.Where(x => $".{GetPackageInfo(x).Name}".EndsWith($".{triggerName}", StringComparison.OrdinalIgnoreCase)).ToList();
							if (matches.Count == 0)
								throw new ApplicationException($"Trigger '{trigger}' does not match any packages: {string.Join(", ", packagePaths.Select(Path.GetFileName))}");
							if (matches.Count > 1)
								throw new ApplicationException($"Trigger '{trigger}' matches multiple package(s): {string.Join(", ", matches.Select(Path.GetFileName))}");
							if (GetPackageInfo(matches[0]).Version != triggerVersion)
								throw new ApplicationException($"Trigger '{trigger}' doesn't match package version: {Path.GetFileName(matches[0])}");
							packagePaths = matches;
						}

						shouldPublishPackages = true;
						shouldPublishDocs = !triggerMatch.Groups["suffix"].Success;
					}

					if (shouldPublishPackages || shouldPublishDocs)
					{
						var docsSettings = settings.DocsSettings;
						bool shouldPushDocs = false;
						string cloneDirectory = null;
						string repoDirectory = null;
						string gitBranchName = null;

						Credentials provideCredentials(string url, string usernameFromUrl, SupportedCredentialTypes types) =>
							new UsernamePasswordCredentials
							{
								Username = docsSettings.GitLogin.Username ?? throw new ApplicationException("GitLogin has a null Username."),
								Password = docsSettings.GitLogin.Password ?? throw new ApplicationException("GitLogin has a null Password."),
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

							using (var repository = new Repository(repoDirectory))
							{
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
										if (autoBranchName == null)
											throw new ArgumentException("Could not determine repository branch.");

										branch = repository.Branches[autoBranchName] ?? repository.CreateBranch(autoBranchName);
										Commands.Checkout(repository, branch);
									}
									gitBranchName = branch.FriendlyName;
								}

								foreach (var projectName in packagePaths.Select(x => GetPackageInfo(x).Name))
								{
									string findAssembly(string name) =>
										FindFiles($"tools/XmlDocTarget/bin/**/{name}.dll").OrderByDescending(File.GetLastWriteTime).FirstOrDefault() ??
										FindFiles($"src/{name}/bin/**/{name}.dll").OrderByDescending(File.GetLastWriteTime).FirstOrDefault();

									var assemblyPath = (docsSettings.FindAssembly ?? findAssembly)(projectName);
									if (assemblyPath != null)
									{
										RunApp(dotNetTools.GetToolPath($"xmldocmd/{xmlDocMarkdownVersion}"), assemblyPath,
											Path.Combine(repoDirectory, docsSettings.TargetDirectory ?? "docs"),
											"--source", $"{docsSettings.SourceCodeUrl}/{projectName}", "--newline", "lf", "--clean");
									}
									else
									{
										Console.WriteLine($"Documentation not generated for {projectName}; assembly not found.");
									}
								}

								shouldPushDocs = repository.RetrieveStatus().IsDirty;
							}
						}

						if (shouldPublishPackages)
						{
							var projectUsesSourceLink = settings.ProjectUsesSourceLink;
							foreach (var packagePath in packagePaths)
							{
								if (projectUsesSourceLink == null || projectUsesSourceLink(GetPackageInfo(packagePath).Name))
									RunApp(dotNetTools.GetToolPath($"sourcelink/{sourceLinkVersion}"), "test", packagePath);
							}

							var nugetApiKey = settings.NuGetApiKey;
							if (string.IsNullOrEmpty(nugetApiKey))
								throw new ApplicationException("NuGetApiKey required to publish.");

							foreach (var packagePath in packagePaths)
								RunDotNet("nuget", "push", packagePath, "--source", nugetSource, "--api-key", nugetApiKey);
						}

						if (shouldPushDocs)
						{
							using (var repository = new Repository(repoDirectory))
							{
								Console.WriteLine("Publishing documentation changes.");
								Commands.Stage(repository, "*");
								var author = new Signature(docsSettings.GitAuthor.Name, docsSettings.GitAuthor.Email, DateTimeOffset.Now);
								repository.Commit("Documentation updated.", author, author, new CommitOptions());
								repository.Network.Push(repository.Network.Remotes["origin"],
									$"refs/heads/{gitBranchName}", new PushOptions { CredentialsProvider = provideCredentials });
							}
						}

						if (cloneDirectory != null)
						{
							// delete the cloned directory
							foreach (var fileInfo in FindFiles(cloneDirectory, "**").Select(x => new FileInfo(x)).Where(x => x.IsReadOnly))
								fileInfo.IsReadOnly = false;
							Directory.Delete(cloneDirectory, recursive: true);
						}
					}
					else
					{
						Console.WriteLine("To publish to NuGet, push a matching git tag for the release.");
					}
				});

			string getPlatformArg()
			{
				string platformValue = platformOption.Value ?? settings?.SolutionPlatform;
				return platformValue == null ? null : $"-p:Platform={platformValue}";
			}

			void runMSBuild(params string[] arguments) =>
				RunMSBuild(msbuildSettings, arguments.Append("-v:normal").Append("-maxcpucount").ToArray());
		}

		private static (string Name, string Version, string Suffix) GetPackageInfo(string path)
		{
			var match = Regex.Match(path, @"[/\\](?<name>[^/\\]+)\.(?<version>[0-9]+\.[0-9]+\.[0-9]+(-(?<suffix>.+))?)\.nupkg$", RegexOptions.ExplicitCapture);
			return (match.Groups["name"].Value, match.Groups["version"].Value, match.Groups["suffix"].Value);
		}

		private static readonly Regex s_triggerRegex = new Regex(@"^((?<name>[^/\\]+)-)?v(?<version>[0-9]+\.[0-9]+\.[0-9]+(-(?<suffix>.+))?)$", RegexOptions.ExplicitCapture);
	}
}
