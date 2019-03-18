using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LibGit2Sharp;
using XmlDocMarkdown.Core;
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
		public static void AddDotNetTargets(this BuildApp build, DotNetBuildSettings settings)
		{
			var configurationOption = build.AddOption("-c|--configuration <name>", "The configuration to build", "Release");
			var nugetApiKeyOption = build.AddOption("--nuget-api-key <name>", "NuGet API key for publishing");
			var versionSuffixOption = build.AddOption("--version-suffix <suffix>", "Generates a prerelease package");
			var triggerOption = build.AddOption("--trigger <name>", "The branch or tag that triggered the build");

			var solutionName = settings.SolutionName;
			var nugetSource = settings.NuGetSource ?? "https://api.nuget.org/v3/index.json";

			var dotNetTools = settings.DotNetTools ?? new DotNetTools(Path.Combine("tools", "bin"));

			const string docsBranchName = "gh-pages";

			build.Target("clean")
				.Describe("Deletes all build output")
				.Does(() =>
				{
					foreach (var directory in FindDirectories("{src,tests}/**/{bin,obj}", "release"))
						Directory.Delete(directory, recursive: true);
				});

			build.Target("build")
				.Describe("Builds the solution")
				.Does(() => RunDotNet("build", solutionName, "-c", configurationOption.Value, "--verbosity", "normal"));

			build.Target("test")
				.DependsOn("build")
				.Describe("Runs the unit tests")
				.Does(() => RunDotNet("test", solutionName, "-c", configurationOption.Value, "--no-build"));

			build.Target("package")
				.DependsOn("clean", "test")
				.Describe("Builds the NuGet packages")
				.Does(() =>
				{
					string versionSuffix = versionSuffixOption.Value;
					string trigger = triggerOption.Value;
					if (versionSuffix == null && trigger != null)
					{
						var group = Regex.Match(trigger, @"^v[^\.]+\.[^\.]+\.[^\.]+-(.+)").Groups[1];
						if (group.Success)
							versionSuffix = group.ToString();
					}

					RunDotNet("pack", solutionName,
						"-c", configurationOption.Value,
						"--no-build",
						"--output", Path.GetFullPath("release"),
						versionSuffix != null ? "--version-suffix" : null, versionSuffix);
				});

			build.Target("package-test")
				.DependsOn("package")
				.Describe("Tests the NuGet packages")
				.Does(() =>
				{
					string sourcelink = dotNetTools.GetToolPath("sourcelink");
					foreach (var packagePath in FindFiles("release/*.nupkg"))
						RunApp(sourcelink, "test", packagePath);
				});

			build.Target("docs")
				.DependsOn("build")
				.Describe("Generates reference documentation")
				.Does(() =>
				{
					var docsSettings = settings.XmlDocMarkdownSettings;
					if (docsSettings != null)
					{
						if (!Directory.Exists(docsBranchName))
							Repository.Clone(docsSettings.RepoUrl, docsBranchName, new CloneOptions { BranchName = docsBranchName });

						foreach (string docsProject in docsSettings.Projects)
						{
							string dllPath = FindFiles($"src/{docsProject}/bin/**/{docsProject}.dll").First();
							XmlDocMarkdownGenerator.Generate(dllPath, $"{docsBranchName}/",
								new XmlDocMarkdown.Core.XmlDocMarkdownSettings { SourceCodePath = $"{docsSettings.SourceUrl}/{docsProject}", NewLine = "\n", ShouldClean = true });
						}
					}
				});

			build.Target("publish")
				.DependsOn("package-test", "docs")
				.Describe("Publishes the NuGet packages")
				.Does(() =>
				{
					var nupkgPaths = FindFiles("release/*.nupkg");

					string version = null;
					foreach (var nupkgPath in nupkgPaths)
					{
						string nupkgVersion = Regex.Match(nupkgPath, @"\.([^\.]+\.[^\.]+\.[^\.]+)\.nupkg$").Groups[1].ToString();
						if (version == null)
							version = nupkgVersion;
						else if (version != nupkgVersion)
							throw new InvalidOperationException($"Mismatched package versions '{version}' and '{nupkgVersion}'.");
					}

					var nugetApiKey = nugetApiKeyOption.Value;
					var trigger = triggerOption.Value;
					if (version != null && nugetApiKey != null && (trigger == null || Regex.IsMatch(trigger, "^v[0-9]")))
					{
						if (trigger != null && trigger != $"v{version}")
							throw new InvalidOperationException($"Trigger '{trigger}' doesn't match package version '{version}'.");
						foreach (var nupkgPath in nupkgPaths)
							RunDotNet("nuget", "push", nupkgPath, "--source", nugetSource, "--api-key", nugetApiKey);

						if (settings.GitLogin != null &&
							settings.GitAuthor != null &&
							Directory.Exists(docsBranchName) &&
							Environment.GetEnvironmentVariable("APPVEYOR_REPO_BRANCH") == "master" &&
							!version.Contains("-"))
						{
							using (var repository = new Repository(docsBranchName))
							{
								if (repository.RetrieveStatus().IsDirty)
								{
									Console.WriteLine("Publishing documentation changes.");
									Commands.Stage(repository, "*");
									var author = new Signature(settings.GitAuthor.Name, settings.GitAuthor.Email, DateTimeOffset.Now);
									repository.Commit(message: $"Automatic documentation update for {version}.", author, author, new CommitOptions());
									var credentials = new UsernamePasswordCredentials { Username = settings.GitLogin.Username, Password = settings.GitLogin.Password };
									repository.Network.Push(repository.Branches, new PushOptions { CredentialsProvider = (_, __, ___) => credentials });
								}
								else
								{
									Console.WriteLine("No documentation changes detected.");
								}
							}
						}
						else
						{
							Console.WriteLine("Documentation not published for this build.");
						}
					}
					else
					{
						Console.WriteLine($"To publish this package, push this git tag: v{version}");
					}
				});
		}
	}
}
