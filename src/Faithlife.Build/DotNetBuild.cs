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
		public static void AddDotNetTargets(this BuildApp build, DotNetBuildSettings settings = null)
		{
			var configurationOption = build.AddOption("-c|--configuration <name>", "The configuration to build (default Release)", "Release");
			var nugetApiKeyOption = build.AddOption("--nuget-api-key <name>", "NuGet API key for publishing");
			var versionSuffixOption = build.AddOption("--version-suffix <suffix>", "Generates a prerelease package");
			var triggerOption = build.AddOption("--trigger <name>", "The git branch or tag that triggered the build");
			var branchOption = build.AddOption("--branch <name>", "The git branch being built (for docs updates)");

			settings = settings ?? new DotNetBuildSettings();
			var solutionName = settings.SolutionName;
			var nugetSource = settings.NuGetSource ?? "https://api.nuget.org/v3/index.json";

			var dotNetTools = settings.DotNetTools ?? new DotNetTools(Path.Combine("tools", "bin"));

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
				.DependsOn("test")
				.Describe("Builds the NuGet package")
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

			build.Target("publish")
				.Describe("Publishes the NuGet package and documentation")
				.DependsOn("clean", "package")
				.Does(() =>
				{
					var packagePaths = FindFiles("release/*.nupkg");
					if (packagePaths.Count != 1)
						throw new InvalidOperationException($"{packagePaths.Count} NuGet packages found.");
					var packagePath = packagePaths[0];

					RunApp(dotNetTools.GetToolPath("sourcelink"), "test", packagePath);

					var packageName = Path.GetFileName(packagePath) ?? "";
					string[] packageNameParts = packageName.Split('.');
					string projectName = string.Join(".", packageNameParts.Take(packageNameParts.Length - 4));
					string version = string.Join(".", packageNameParts.Skip(packageNameParts.Length - 4).Take(3));

					var nugetApiKey = nugetApiKeyOption.Value;
					if (nugetApiKey == null)
						throw new InvalidOperationException("--nuget-api-key option required to publish.");

					var trigger = triggerOption.Value;
					if (trigger == null || Regex.IsMatch(trigger, "^v[0-9]"))
					{
						if (trigger != null && trigger != $"v{version}")
							throw new InvalidOperationException($"Trigger '{trigger}' doesn't match package version '{version}'.");
						RunDotNet("nuget", "push", packagePath, "--source", nugetSource, "--api-key", nugetApiKey);

						string branchName = branchOption.Value;
						if (settings.GitLogin != null && settings.GitAuthor != null && branchName != null && !version.Contains("-"))
						{
							using (var repository = new Repository("."))
							{
								var branch = repository.CreateBranch(branchName, $"origin/{branchName}");
								Commands.Checkout(repository, branch);

								string dllPath = FindFiles($"src/{projectName}/bin/**/{projectName}.dll").First();
								XmlDocMarkdownGenerator.Generate(dllPath, "docs/",
									new XmlDocMarkdownSettings { SourceCodePath = $"{settings.SourceCodeUrl}/{projectName}", NewLine = "\n", ShouldClean = true });

								if (repository.RetrieveStatus().IsDirty)
								{
									Console.WriteLine("Publishing documentation changes.");
									Commands.Stage(repository, "*");
									var author = new Signature(settings.GitAuthor.Name, settings.GitAuthor.Email, DateTimeOffset.Now);
									repository.Commit($"Documentation updated for {version}.", author, author, new CommitOptions());
									var credentials = new UsernamePasswordCredentials { Username = settings.GitLogin.Username, Password = settings.GitLogin.Password };
									repository.Network.Push(repository.Network.Remotes["origin"],
										"master", "origin/master", new PushOptions { CredentialsProvider = (_, __, ___) => credentials });
								}
								else
								{
									Console.WriteLine("No documentation changes detected.");
								}
							}
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
