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
			var branchOption = buildOptions.BranchOption ?? (buildOptions.BranchOption =
				build.AddOption("--branch <name>", "The git branch being built (for docs updates)"));

			var solutionName = settings.SolutionName;
			var nugetSource = settings.NuGetSource ?? "https://api.nuget.org/v3/index.json";

			var dotNetTools = settings.DotNetTools ?? new DotNetTools(Path.Combine("tools", "bin"));
			var sourceLinkVersion = settings.SourceLinkToolVersion ?? "3.0.0";
			var xmlDocMarkdownVersion = settings.DocsSettings?.ToolVersion ?? "1.5.1";

			build.Target("clean")
				.Describe("Deletes all build output")
				.Does(() =>
				{
					foreach (var directory in FindDirectories("{src,tests}/**/{bin,obj}", "release"))
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
				.Describe("Builds the NuGet package")
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
				.Describe("Publishes the NuGet package and documentation")
				.DependsOn("clean", "package")
				.Does(() =>
				{
					var nugetApiKey = nugetApiKeyOption.Value;
					if (nugetApiKey == null)
						throw new InvalidOperationException("--nuget-api-key option required to publish.");

					var trigger = triggerOption.Value;
					if (trigger == null)
						throw new InvalidOperationException("--trigger option required to publish.");

					var packagePaths = FindFiles("release/*.nupkg");
					if (packagePaths.Count == 0)
						throw new InvalidOperationException("No NuGet packages found.");

					var triggerMatch = s_triggerRegex.Match(trigger);
					var onlyUpdateDocs = trigger == "update-docs";
					if (triggerMatch.Success || onlyUpdateDocs)
					{
						var publishPackage = triggerMatch.Groups["name"].Value;
						var publishVersion = triggerMatch.Groups["version"].Value;

						if (!onlyUpdateDocs)
						{
							if (publishPackage.Length == 0)
							{
								var mismatches = packagePaths.Where(x => GetPackageInfo(x).Version != publishVersion).ToList();
								if (mismatches.Count != 0)
									throw new InvalidOperationException($"Trigger '{trigger}' doesn't match package version: {string.Join(", ", mismatches.Select(Path.GetFileName))}");
							}
							else
							{
								var matches = packagePaths.Where(x => $".{GetPackageInfo(x).Name}".EndsWith($".{publishPackage}", StringComparison.OrdinalIgnoreCase)).ToList();
								if (matches.Count == 0)
									throw new InvalidOperationException($"Trigger '{trigger}' does not match any packages: {string.Join(", ", packagePaths.Select(Path.GetFileName))}");
								if (matches.Count > 1)
									throw new InvalidOperationException($"Trigger '{trigger}' matches multiple package(s): {string.Join(", ", matches.Select(Path.GetFileName))}");
								if (GetPackageInfo(matches[0]).Version != publishVersion)
									throw new InvalidOperationException($"Trigger '{trigger}' doesn't match package version: {Path.GetFileName(matches[0])}");
								packagePaths = matches;
							}
						}

						string branchName = branchOption.Value;
						var docsSettings = settings.DocsSettings;
						bool shouldPublishDocs = false;
						if (docsSettings != null && branchName != null && (onlyUpdateDocs || !publishVersion.Contains("-")))
						{
							if (docsSettings.GitLogin == null || docsSettings.GitAuthor == null)
								throw new InvalidOperationException("GitLogin and GitAuthor must be set on DocumentationSettings.");

							using (var repository = new Repository("."))
							{
								var branch = repository.Branches[branchName] ?? repository.CreateBranch(branchName);
								Commands.Checkout(repository, branch);

								foreach (var projectName in packagePaths.Select(x => GetPackageInfo(x).Name))
								{
									var dllPaths = FindFiles($"src/{projectName}/bin/**/{(docsSettings.TargetFramework != null ? $"{docsSettings.TargetFramework}/" : "")}{projectName}.dll")
										.OrderByDescending(x => x, StringComparer.Ordinal).ToList();
									if (dllPaths.Count != 0)
									{
										RunApp(dotNetTools.GetToolPath($"xmldocmd/{xmlDocMarkdownVersion}"), dllPaths[0], docsSettings.TargetDirectory ?? "docs",
											"--source", $"{docsSettings.SourceCodeUrl}/{projectName}", "--newline", "lf", "--clean");
									}
									else
									{
										Console.WriteLine($"Documentation not generated for {projectName}; no DLL found.");
									}
								}

								shouldPublishDocs = repository.RetrieveStatus().IsDirty;
							}
						}

						if (!onlyUpdateDocs)
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

						if (shouldPublishDocs)
						{
							using (var repository = new Repository("."))
							{
								Console.WriteLine("Publishing documentation changes.");
								Commands.Stage(repository, "*");
								var author = new Signature(docsSettings.GitAuthor.Name, docsSettings.GitAuthor.Email, DateTimeOffset.Now);
								repository.Commit($"Documentation updated for {trigger}.", author, author, new CommitOptions());
								var credentials = new UsernamePasswordCredentials { Username = docsSettings.GitLogin.Username, Password = docsSettings.GitLogin.Password };
								repository.Network.Push(repository.Network.Remotes["origin"],
									$"refs/heads/{branchName}", new PushOptions { CredentialsProvider = (_, __, ___) => credentials });
							}
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
