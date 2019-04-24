using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LibGit2Sharp;
using static Faithlife.Build.AppRunner;
using static Faithlife.Build.BuildUtility;
using static Faithlife.Build.DotNetRunner;

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
			var nugetApiKeyOption = buildOptions.NuGetApiKeyOption ?? (buildOptions.NuGetApiKeyOption =
				build.AddOption("--nuget-api-key <name>", "NuGet API key for publishing"));
			var versionSuffixOption = buildOptions.VersionSuffixOption ?? (buildOptions.VersionSuffixOption =
				build.AddOption("--version-suffix <suffix>", "Generates a prerelease package"));
			var nugetOutputOption = buildOptions.NuGetOutputOption ?? (buildOptions.NuGetOutputOption =
				build.AddOption("--nuget-output <path>", "Directory for generated package (default release)", "release"));
			var triggerOption = buildOptions.TriggerOption ?? (buildOptions.TriggerOption =
				build.AddOption("--trigger <name>", "The git branch or tag that triggered the build"));

			var solutionName = settings.SolutionName;
			var nugetSource = settings.NuGetSource ?? "https://api.nuget.org/v3/index.json";

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
				.Does(() => RunDotNet("restore", solutionName, "--verbosity", "normal"));

			build.Target("build")
				.DependsOn("restore")
				.Describe("Builds the solution")
				.Does(() => RunDotNet("build", solutionName, "-c", configurationOption.Value, "--no-restore", "--verbosity", "normal"));

			build.Target("test")
				.DependsOn("build")
				.Describe("Runs the unit tests")
				.Does(() => RunDotNet("test", solutionName, "-c", configurationOption.Value, "--no-build"));

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

					RunDotNet("pack", solutionName,
						"-c", configurationOption.Value,
						"--no-build",
						"--output", Path.GetFullPath(nugetOutputOption.Value),
						versionSuffix != null ? "--version-suffix" : null, versionSuffix);
				});

			build.Target("publish")
				.Describe("Publishes NuGet packages and documentation")
				.DependsOn("clean", "package")
				.Does(() =>
				{
					var nugetApiKey = nugetApiKeyOption.Value;
					if (nugetApiKey == null)
						throw new InvalidOperationException("--nuget-api-key option required.");

					var trigger = triggerOption.Value;
					if (trigger == null)
						throw new InvalidOperationException("--trigger option required.");

					var packagePaths = FindFilesFrom(Path.GetFullPath(nugetOutputOption.Value), "*.nupkg");
					if (packagePaths.Count == 0)
						throw new InvalidOperationException("No NuGet packages found.");

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
								throw new InvalidOperationException($"Trigger '{trigger}' doesn't match package version: {string.Join(", ", mismatches.Select(Path.GetFileName))}");
						}
						else
						{
							var matches = packagePaths.Where(x => $".{GetPackageInfo(x).Name}".EndsWith($".{triggerName}", StringComparison.OrdinalIgnoreCase)).ToList();
							if (matches.Count == 0)
								throw new InvalidOperationException($"Trigger '{trigger}' does not match any packages: {string.Join(", ", packagePaths.Select(Path.GetFileName))}");
							if (matches.Count > 1)
								throw new InvalidOperationException($"Trigger '{trigger}' matches multiple package(s): {string.Join(", ", matches.Select(Path.GetFileName))}");
							if (GetPackageInfo(matches[0]).Version != triggerVersion)
								throw new InvalidOperationException($"Trigger '{trigger}' doesn't match package version: {Path.GetFileName(matches[0])}");
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
						if (shouldPublishDocs && docsSettings != null)
						{
							if (docsSettings.GitLogin == null || docsSettings.GitAuthor == null)
								throw new InvalidOperationException("GitLogin and GitAuthor must be set on DocumentationSettings.");
							if (docsSettings.GitRepositoryUrl == null || docsSettings.GitBranchName == null)
								throw new InvalidOperationException("GitRepositoryUrl, GitBranchName, and docsSettings.GitCloneDirectory must be set on DocumentationSettings.");

							cloneDirectory = "docs_repo_" + Path.GetRandomFileName();
							Repository.Clone(sourceUrl: docsSettings.GitRepositoryUrl, workdirPath: cloneDirectory,
								options: new CloneOptions { BranchName = docsSettings.GitBranchName });

							using (var repository = new Repository(cloneDirectory))
							{
								foreach (var projectName in packagePaths.Select(x => GetPackageInfo(x).Name))
								{
									var dllPaths = FindFiles($"src/{projectName}/bin/**/{(docsSettings.TargetFramework != null ? $"{docsSettings.TargetFramework}/" : "")}{projectName}.dll")
										.OrderByDescending(x => x, StringComparer.Ordinal).ToList();
									if (dllPaths.Count != 0)
									{
										RunApp(dotNetTools.GetToolPath($"xmldocmd/{xmlDocMarkdownVersion}"), dllPaths[0],
											Path.Combine(cloneDirectory, docsSettings.TargetDirectory ?? "docs"),
											"--source", $"{docsSettings.SourceCodeUrl}/{projectName}", "--newline", "lf", "--clean");
									}
									else
									{
										Console.WriteLine($"Documentation not generated for {projectName}; no DLL found.");
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

							foreach (var packagePath in packagePaths)
								RunDotNet("nuget", "push", packagePath, "--source", nugetSource, "--api-key", nugetApiKey);
						}

						if (shouldPushDocs)
						{
							using (var repository = new Repository(cloneDirectory))
							{
								Console.WriteLine("Publishing documentation changes.");
								Commands.Stage(repository, "*");
								var author = new Signature(docsSettings.GitAuthor.Name, docsSettings.GitAuthor.Email, DateTimeOffset.Now);
								repository.Commit("Documentation updated.", author, author, new CommitOptions());
								var credentials = new UsernamePasswordCredentials { Username = docsSettings.GitLogin.Username, Password = docsSettings.GitLogin.Password };
								repository.Network.Push(repository.Branches, new PushOptions { CredentialsProvider = (_, __, ___) => credentials });
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
		}

		private static (string Name, string Version, string Suffix) GetPackageInfo(string path)
		{
			var match = Regex.Match(path, @"[/\\](?<name>[^/\\]+)\.(?<version>[0-9]+\.[0-9]+\.[0-9]+(-(?<suffix>.+))?)\.nupkg$", RegexOptions.ExplicitCapture);
			return (match.Groups["name"].Value, match.Groups["version"].Value, match.Groups["suffix"].Value);
		}

		private static readonly Regex s_triggerRegex = new Regex(@"^((?<name>[^/\\]+)-)?v(?<version>[0-9]+\.[0-9]+\.[0-9]+(-(?<suffix>.+))?)$", RegexOptions.ExplicitCapture);
	}
}
