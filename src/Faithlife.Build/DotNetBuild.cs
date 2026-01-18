using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
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

namespace Faithlife.Build;

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
					(() => [.. FindDirectories("{src,tests,tools}/**/{bin,obj}").Except(FindDirectories("tools/bin", "**/node_modules/**/bin"))]);
				foreach (var directoryToDelete in findDirectoriesToDelete())
					DeleteDirectory(directoryToDelete);

				var extraProperties = settings.GetExtraPropertyArgs("clean");
				if (msbuildSettings is null)
					RunDotNet(new[] { "clean", solutionName, "-c", settings.GetConfiguration(), settings.GetPlatformArg(), settings.GetBuildNumberArg(), settings.GetVerbosityArg(), settings.GetMaxCpuCountArg() }.Concat(extraProperties));
				else
					MSBuild(new[] { solutionName, "-t:Clean", settings.GetConfigurationArg(), settings.GetPlatformArg(), settings.GetBuildNumberArg(), settings.GetVerbosityArg(), settings.GetMaxCpuCountArg() }.Concat(extraProperties));
			});

		build.Target("restore")
			.Describe("Restores NuGet packages")
			.Does(() =>
			{
				using var runtimeTargetsFile = RuntimeTargetsFile.Create(settings);
				var extraProperties = settings.GetExtraPropertyArgs("restore");
				if (msbuildSettings is null)
					RunDotNet(new[] { "restore", solutionName, settings.GetPlatformArg(), settings.GetBuildNumberArg(), settings.GetVerbosityArg(), settings.GetMaxCpuCountArg(), runtimeTargetsFile.GetBuildArg() }.Concat(extraProperties));
				else
					MSBuild(new[] { solutionName, "-t:Restore", settings.GetConfigurationArg(), settings.GetPlatformArg(), settings.GetBuildNumberArg(), settings.GetVerbosityArg(), settings.GetMaxCpuCountArg(), runtimeTargetsFile.GetBuildArg() }.Concat(extraProperties));

				if (DotNetLocalTool.Any())
					RunDotNet("tool", "restore");
			});

		build.Target("build")
			.DependsOn("restore")
			.Describe("Builds the solution")
			.Does(() =>
			{
				using var runtimeTargetsFile = RuntimeTargetsFile.Create(settings);
				var extraProperties = settings.GetExtraPropertyArgs("build");
				if (msbuildSettings is null)
					RunDotNet(new[] { "build", solutionName, "-c", settings.GetConfiguration(), settings.GetPlatformArg(), settings.GetBuildNumberArg(), "--no-restore", settings.GetVerbosityArg(), settings.GetMaxCpuCountArg(), settings.GetBuildSummaryArg(), runtimeTargetsFile.GetBuildArg() }.Concat(extraProperties));
				else
					MSBuild(new[] { solutionName, settings.GetConfigurationArg(), settings.GetPlatformArg(), settings.GetBuildNumberArg(), settings.GetVerbosityArg(), settings.GetMaxCpuCountArg(), settings.GetBuildSummaryArg(), runtimeTargetsFile.GetBuildArg() }.Concat(extraProperties));
			});

		build.Target("test")
			.DependsOn("build")
			.Describe("Runs the unit tests")
			.Does(() =>
			{
				if (buildOptions.NoTestFlag.Value)
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
						testPaths = [solutionName];

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
			var trigger = buildOptions.TriggerOption.Value;

			if (trigger == "detect")
			{
				using var repository = OpenRepository(".");
				var headSha = repository.Head.Tip.Sha;
				var autoTrigger = GetBestTriggerFromTags([.. repository.Tags.Where(x => x.Target.Sha == headSha).Select(x => x.FriendlyName)]);
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

			var versionSuffix = buildOptions.VersionSuffixOption.Value;
			if (versionSuffix is null && trigger is not null)
				versionSuffix = GetVersionFromTrigger(trigger) is { } triggerVersion ? SplitVersion(triggerVersion).Suffix : null;

			var nugetOutputPath = Path.GetFullPath(buildOptions.NuGetOutputOption.Value!);
			var tempOutputPath = Path.Combine(nugetOutputPath, Path.GetRandomFileName());

			var packageProjects = new List<string?>();

			var findPackageProjects = settings.PackageSettings?.FindProjects;
			if (findPackageProjects is not null)
				packageProjects.AddRange(findPackageProjects());
			else
				packageProjects.Add(solutionName);

			using var runtimeTargetsFile = RuntimeTargetsFile.Create(settings);
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
						settings.GetBuildNumberArg(),
						"--no-build",
						"--output", tempOutputPath,
						versionSuffix is not null ? "--version-suffix" : null, versionSuffix,
						settings.GetMaxCpuCountArg(),
						runtimeTargetsFile.GetBuildArg(),
					}.Concat(extraProperties));
				}
				else
				{
					MSBuild(new[]
					{
						packageProject, "-t:Pack",
						settings.GetConfigurationArg(),
						settings.GetPlatformArg(),
						settings.GetBuildNumberArg(),
						"-p:NoBuild=true",
						$"-p:PackageOutputPath={tempOutputPath}",
						versionSuffix is not null ? $"-p:VersionSuffix={versionSuffix}" : null,
						settings.GetVerbosityArg(),
						settings.GetMaxCpuCountArg(),
						runtimeTargetsFile.GetBuildArg(),
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
				if (buildOptions.TriggerOption.Value == "publish-nuget-output")
				{
					// '--trigger publish-nuget-output' can be used with '--skip package' to publish the output of a previous build
					var nugetOutputPath = Path.GetFullPath(buildOptions.NuGetOutputOption.Value!);
					packagePaths = FindFilesFrom(nugetOutputPath, "*.nupkg");
					DoPublish(canPublishDocs: false);
				}
				else
				{
					// we must build the packages to identify them
					packagePaths ??= BuildNuGetPackages();
					DoPublish(canPublishDocs: true);
				}
			});

		void DoPublish(bool canPublishDocs)
		{
			if (packagePaths.Count == 0)
				throw new BuildException("No NuGet packages found.");

			if (packagePaths.Any(x => GetPackageInfo(x).Version == "0.0.0"))
			{
				Console.WriteLine("Not publishing package with version 0.0.0. Change package version to publish.");
				return;
			}

			var (trigger, triggerAutoDetected) = GetTrigger();
			trigger ??= "publish-all";

			var triggerParts = trigger.Split('-', 2);
			var publishTrigger = triggerParts.Length >= 2 && triggerParts[0] == "publish" ? triggerParts[1] : null;
			var shouldPublishPackages = publishTrigger is "package" or "packages" or "all" or "nuget-output";
			var shouldPublishDocs = canPublishDocs && publishTrigger is "docs" or "all" or "nuget-output";

			var triggerVersion = GetVersionFromTrigger(trigger);
			if (triggerVersion is not null)
			{
				var mismatches = packagePaths.Where(x => GetPackageInfo(x).Version != triggerVersion).ToList();
				if (mismatches.Count != 0)
					throw new BuildException($"Trigger '{trigger}' doesn't match package version: {string.Join(", ", mismatches.Select(Path.GetFileName))}");

				shouldPublishPackages = true;
				shouldPublishDocs = canPublishDocs && !triggerVersion.Contains('-', StringComparison.Ordinal);
			}

			if (shouldPublishPackages || shouldPublishDocs)
			{
				var docsSettings = settings.DocsSettings;
				var shouldPushDocs = false;
				string? docsCloneDirectory = null;
				string? docsRepoDirectory = null;
				string? docsGitBranchName = null;

				if (shouldPublishDocs && docsSettings is not null)
				{
					if (docsSettings.GitLogin is null || docsSettings.GitAuthor is null)
						throw new BuildException("GitLogin and GitAuthor must be set to publish documentation.");

					var gitRepositoryUrl = docsSettings.GitRepositoryUrl;
					docsGitBranchName = docsSettings.GitBranchName;

					if (gitRepositoryUrl is not null || docsGitBranchName is not null)
					{
						docsCloneDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
						var sourceUrl = gitRepositoryUrl;
						try
						{
							// determine if the local working directory has the same 'origin' remote URL as the docs git repository; if so, clone from the local folder (which should be much faster)
							string? localRepositorySource = null;
							try
							{
								// allow the local repo to be cloned if it has the same remote URL (ignoring trailing .git) and branch (because it may be a shallow clone)
								using var localRepo = OpenRepository(".");
								var originUrl = localRepo.Network.Remotes["origin"].Url;
								gitRepositoryUrl ??= originUrl;
								var trimTrailingDotGit = new Regex(@"\.git$");
								if (trimTrailingDotGit.Replace(originUrl, "") != trimTrailingDotGit.Replace(gitRepositoryUrl, ""))
									Console.WriteLine($"Local repository in {localRepo.Info.WorkingDirectory} does not have the same remote URL as the docs repository: {originUrl} != {gitRepositoryUrl}");
								else if (localRepo.Head.FriendlyName != docsGitBranchName)
									Console.WriteLine($"Local repository in {localRepo.Info.WorkingDirectory} does not have the same branch as the docs repository: {localRepo.Head.FriendlyName} != {docsGitBranchName}");
								else
									localRepositorySource = localRepo.Info.WorkingDirectory;
							}
							catch (BuildException)
							{
							}

							sourceUrl = localRepositorySource ?? gitRepositoryUrl;
							Console.WriteLine($"Cloning documentation repository from {sourceUrl} to {docsCloneDirectory}");
							Repository.Clone(sourceUrl: sourceUrl, workdirPath: docsCloneDirectory,
								options: new CloneOptions { BranchName = docsGitBranchName, FetchOptions = { CredentialsProvider = ProvideDocsCredentials } });

							if (localRepositorySource is not null)
							{
								// if the local repo was cloned, update the 'origin' remote so that changes are pushed to the correct remote
								using var clonedRepo = OpenRepository(docsCloneDirectory);
								clonedRepo.Network.Remotes.Remove("origin");
								clonedRepo.Network.Remotes.Add("origin", gitRepositoryUrl);
							}
						}
						catch (LibGit2SharpException exception)
						{
							throw new BuildException($"Failed to clone {sourceUrl} branch {docsGitBranchName} to {docsCloneDirectory}{GetGitLoginErrorMessage(docsSettings.GitLogin!)}: {exception.Message}");
						}
						docsRepoDirectory = docsCloneDirectory;
					}
					else
					{
						docsRepoDirectory = ".";
					}

					using var repository = OpenRepository(docsRepoDirectory);
					if (gitRepositoryUrl is not null)
					{
						docsGitBranchName ??= repository.Head.FriendlyName;
					}
					else if (docsGitBranchName is not null)
					{
						if (docsGitBranchName != repository.Head.FriendlyName)
						{
							Console.WriteLine($"Checking out branch {docsGitBranchName}.");
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
								if (gitRef?.StartsWith(prefix, StringComparison.Ordinal) is true)
									autoBranchName = gitRef[prefix.Length..];
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

					var xmlDocGenPaths = new List<string>();
					var xmlDocGenProjectPath = FindFiles("tools/XmlDocGen/XmlDocGen.csproj").FirstOrDefault();
					if (xmlDocGenProjectPath is not null)
					{
						var xmlDocGenProjectDocument = XDocument.Load(xmlDocGenProjectPath);
						var xmlDocGenFrameworks = GetTargetFrameworks();
						foreach (var framework in xmlDocGenFrameworks)
						{
							RunDotNet("publish", xmlDocGenProjectPath,
								framework.Length != 0 ? "--framework" : null, framework.Length != 0 ? framework : null,
								"-c", settings.GetConfiguration(),
								settings.GetPlatformArg(),
								settings.GetBuildNumberArg(),
								"--nologo",
								"--verbosity", "quiet",
								"--output", Path.Combine("tools", "bin", framework, "XmlDocGen"));
							var xmlDocGenPath = Path.Combine("tools", "bin", framework, "XmlDocGen", "XmlDocGen.dll");
							if (!File.Exists(xmlDocGenPath))
								xmlDocGenPath = Path.Combine("tools", "bin", framework, "XmlDocGen", "XmlDocGen.exe");
							if (!File.Exists(xmlDocGenPath))
								throw new BuildException($"Failed to build XmlDocGen{(framework.Length == 0 ? "" : $" ({framework})")}.");
							xmlDocGenPaths.Add(xmlDocGenPath);
						}

						string[] GetTargetFrameworks()
						{
							// return single empty framework unless there are more than one
							var text = xmlDocGenProjectDocument.XPathSelectElements("Project/PropertyGroup/TargetFrameworks").FirstOrDefault()?.Value;
							var values = text is null ? [] : text.Split(';').Select(x => x.Trim()).Where(x => x.Length != 0).ToArray();
							return values.Length > 1 ? values : [""];
						}
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
						else if (xmlDocGenPaths.Count != 0)
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
							if (xmlDocGenPaths.Count != 0)
							{
								foreach (var xmlDocGenPath in xmlDocGenPaths)
								{
									var isDotNetApp = string.Equals(Path.GetExtension(xmlDocGenPath), ".dll", StringComparison.OrdinalIgnoreCase);
									foreach (var assemblyPath in assemblyPaths)
									{
										var actualAssemblyPath = Path.Combine(Path.GetDirectoryName(xmlDocGenPath)!, assemblyPath + ".dll");
										if (File.Exists(actualAssemblyPath))
										{
											if (isDotNetApp)
												RunDotNet(new[] { xmlDocGenPath }.Concat(GetXmlDocArgs(assemblyPath)));
											else
												RunApp(xmlDocGenPath, new AppRunnerSettings { Arguments = GetXmlDocArgs(assemblyPath), IsFrameworkApp = true });
										}
										else if (xmlDocGenPaths.Count == 1)
										{
											// if XmlDocGen has only one framework, this is probably an error
											Console.WriteLine($"Project {project.Name} missing from XmlDocGen: {actualAssemblyPath}");
										}
									}
								}
							}
							else if (DotNetLocalTool.TryCreate("xmldocmd") is { } xmldocmd)
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
							[input, docsPath, "--source", $"{docsSettings.SourceCodeUrl}/{project.Name}", "--newline", "lf", "--clean", string.IsNullOrEmpty(project.Suffix) ? null : "--dryrun"];
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
							packagePaths = [.. packagePaths.Except(alreadyPushedPackages)];
					}

					var tagsToPush = new HashSet<string>();
					var packageSettings = settings.PackageSettings;
					List<string>? signingArguments = null;
					var pushSuccess = false;

					if (packagePaths.Count != 0 && packageSettings?.SigningSettings is { } signingSettings)
					{
						// build the arguments for 'dotnet sign'
						signingArguments = ["sign", "code"];
						switch (signingSettings)
						{
							case { AzureKeyVaultSettings: null, TrustedSigningSettings: null }:
								throw new BuildException("Either TrustedSigningSettings or AzureKeyVaultSettings must be specified.");
							case { AzureKeyVaultSettings: { } azureSettings, TrustedSigningSettings: null }:
								signingArguments.AddRange([
									"azure-key-vault",
									"-kvu", azureSettings.KeyVaultUrl?.AbsoluteUri ?? throw new BuildException("SigningSettings.AzureKeyVaultSettings.KeyVaultUrl is required."),
									"-kvc", azureSettings.CertificateName ?? throw new BuildException("SigningSettings.AzureKeyVaultSettings.CertificateName is required."),
									]);
								break;
							case { AzureKeyVaultSettings: null, TrustedSigningSettings: { } trustedSettings }:
								signingArguments.AddRange([
									"trusted-signing",
									"-act", "azure-cli",
									"-tse", trustedSettings.EndpointUrl?.AbsoluteUri ?? throw new BuildException("SigningSettings.TrustedSigningSettings.EndpointUrl is required."),
									"-tsa", trustedSettings.Account ?? throw new BuildException("SigningSettings.TrustedSigningSettings.Account is required."),
									"-tscp", trustedSettings.CertificateProfile ?? throw new BuildException("SigningSettings.TrustedSigningSettings.CertificateProfile is required."),
									]);
								break;
							default:
								throw new BuildException("Only one of TrustedSigningSettings or AzureKeyVaultSettings can be specified.");
						}

						// install dotnet sign
						RunDotNet("tool", "install", "--tool-path", "release/sign", "--prerelease", "sign");
					}

					foreach (var packagePath in packagePaths)
					{
						if (signingArguments is not null)
						{
							// sign the package before it's published; this will unzip it, sign each file it contains, rezip it, then sign the package as a whole
							RunApp("release/sign/sign", [.. signingArguments, packagePath]);
						}

						var pushArgs = new[]
						{
							"nuget", "push", packagePath,
							"--source", nugetSource,
							"--api-key", nugetApiKey,
							"--skip-duplicate",
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

							if (packageSettings?.PushTagOnPublish is { } getTag &&
								getTag(GetPackageInfo(packagePath)) is { Length: not 0 } tag)
							{
								tagsToPush.Add(tag);
							}
						}
					}

					if (tagsToPush.Count != 0)
					{
						if (Environment.GetEnvironmentVariable("GITHUB_API_URL") is string githubApiUrl)
							PushTagsUsingGitHubApi(packageSettings, tagsToPush, githubApiUrl);
						else
							PushTagsUsingLibGit2(packageSettings, tagsToPush);
					}

					// don't push documentation if the packages have already been published
					if (!pushSuccess)
						shouldPushDocs = false;
				}

				if (shouldPushDocs)
				{
					using var repository = OpenRepository(docsRepoDirectory!);
					Console.WriteLine("Publishing documentation changes.");
					Commands.Stage(repository, "*");
					var author = new Signature(docsSettings!.GitAuthor!.Name, docsSettings.GitAuthor!.Email, DateTimeOffset.Now);
					repository.Commit("Documentation updated.", author, author, new CommitOptions());
					try
					{
						repository.Network.Push(
							remote: repository.Network.Remotes["origin"],
							pushRefSpec: $"refs/heads/{docsGitBranchName}",
							pushOptions: new PushOptions { CredentialsProvider = ProvideDocsCredentials });
					}
					catch (LibGit2SharpException exception)
					{
						throw new BuildException($"Failed to push docs to branch {docsGitBranchName}{GetGitLoginErrorMessage(docsSettings.GitLogin!)}: {exception.Message}");
					}
				}

				if (docsCloneDirectory is not null)
					ForceDeleteDirectory(docsCloneDirectory);

				Credentials ProvideDocsCredentials(string url, string usernameFromUrl, SupportedCredentialTypes types) =>
					new UsernamePasswordCredentials { Username = docsSettings.GitLogin!.Username, Password = docsSettings.GitLogin!.Password };
			}
			else
			{
				Console.WriteLine($"To publish to NuGet, push this tag: v{GetPackageInfo(packagePaths[0]).Version}");
			}
		}

		// allow global tool to be used (dotnet tool update --global jetbrains.resharper.globaltools)
		var jb = DotNetLocalTool.TryCreate("jetbrains.resharper.globaltools");
		void RunJb(IEnumerable<string?> a)
		{
			if (jb is not null)
				jb.Run(a);
			else
				RunApp("jb", a);
		}

		if (jb is not null || FindFiles("*.DotSettings").Count != 0)
		{
			build.Target("cleanup")
				.DependsOn("restore")
				.Describe("Fixes coding style with JetBrains CleanupCode")
				.Does(() =>
				{
					RunJb(
					[
						"cleanupcode",
						"--profile=Build",
						"--verbosity=ERROR",
						"--disable-settings-layers:GlobalAll;GlobalPerProduct;SolutionPersonal;ProjectPersonal",
						.. GetJetBrainsProperties(),
						GetSolutionName(),
					]);
				});

			build.Target("inspect")
				.DependsOn("restore")
				.Describe("Checks coding style with JetBrains InspectCode")
				.Does(() =>
				{
					var outputPath = Path.Combine("release", "inspect.xml");

					RunJb(
						new[]
						{
							"inspectcode",
							"--build",
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
				yield return settings.GetPlatform() is { } platform ? $"--properties:Platform={platform}" : null;
			}
		}

		void MSBuild(IEnumerable<string?> arguments) => RunMSBuild(msbuildSettings, arguments);

		string GetGitCommitSha()
		{
			using var repository = OpenRepository(".");
			return repository.Head.Tip.Sha;
		}

		Repository OpenRepository(string path)
		{
			if (path != ".")
				return new Repository(path);

			var fullPath = Path.GetFullPath(".");
			while (fullPath is not null)
			{
				if (Repository.IsValid(fullPath))
					return new Repository(fullPath);

				fullPath = Path.GetDirectoryName(fullPath);
			}

			throw new BuildException("The current directory is not part of a valid git repository.");
		}

		void PushTagsUsingGitHubApi(DotNetPackageSettings? packageSettings, IEnumerable<string> tagsToPush, string githubApiUrl)
		{
			// authenticate to API, preferring GITHUB_TOKEN environment variable; https://docs.github.com/en/actions/security-guides/automatic-token-authentication
			AuthenticationHeaderValue? authenticationHeader = null;
			if (Environment.GetEnvironmentVariable("GITHUB_TOKEN") is string githubToken)
			{
				authenticationHeader = new AuthenticationHeaderValue("Bearer", githubToken);
			}
			else
			{
				if (packageSettings?.GitLogin is not { } gitLogin)
					throw new BuildException("GITHUB_TOKEN or GitLogin must be set to push tags.");
				var authenticationString = $"{gitLogin.Username}:{gitLogin.Password}";
				authenticationHeader = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(authenticationString)));
			}

			// get the repository (in OWNER/REPO format) from GITHUB_REPOSITORY environment variable
			if (Environment.GetEnvironmentVariable("GITHUB_REPOSITORY") is not string githubRepository)
			{
				// if the environment variable isn't set, assume it can be extracted from the URL
				var url = packageSettings?.GitRepositoryUrl ?? throw new BuildException("GITHUB_REPOSITORY or GitRepositoryUrl must be set to push tags.");
				githubRepository = new Uri(url).AbsolutePath[1..].Replace(".git", "", StringComparison.Ordinal);
			}

			// get SHA for workflow from environment, falling back to local repository
			if (Environment.GetEnvironmentVariable("GITHUB_SHA") is not string commitSha)
				commitSha = GetGitCommitSha();

			// create HTTP client and authenticate to GitHub API
			var httpClient = new HttpClient();
			httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
			httpClient.DefaultRequestHeaders.Authorization = authenticationHeader;
			httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"Faithlife.Build/{Assembly.GetExecutingAssembly().GetName().Version}");

			// https://docs.github.com/en/rest/git/refs#create-a-reference
			var apiUrl = new Uri($"{githubApiUrl}/repos/{githubRepository}/git/refs");

			foreach (var tagToPush in tagsToPush)
			{
				string? message = null;
				HttpResponseMessage? response = null;
				try
				{
					Console.WriteLine($"Pushing git tag {tagToPush} at {commitSha} (using GitHub API).");
					var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
					{
						Content = new StringContent($$"""{"ref":"refs/tags/{{tagToPush}}","sha":"{{commitSha}}"}""", Encoding.UTF8, "application/json"),
					};
					response = httpClient.Send(request);
					if (response.StatusCode != HttpStatusCode.Created)
					{
						using var stream = response.Content.ReadAsStream();
						using var streamReader = new StreamReader(stream);
						message = $"{response.StatusCode}: {streamReader.ReadToEnd()}";
					}
				}
				catch (HttpRequestException ex)
				{
					message = ex.Message;
				}

				if (message is not null)
					throw new BuildException($"Failed to push tag {tagToPush} to {apiUrl.AbsoluteUri}: {message}");
			}
		}

		void PushTagsUsingLibGit2(DotNetPackageSettings? packageSettings, IEnumerable<string> tagsToPush)
		{
			if (packageSettings?.GitLogin is null)
				throw new BuildException("GitLogin must be set to push tags.");

			var commitSha = GetGitCommitSha();

			string? tagsCloneDirectory = null;
			var tagsRepoDirectory = ".";
			var gitRepositoryUrl = packageSettings.GitRepositoryUrl;
			if (gitRepositoryUrl is not null)
			{
				tagsCloneDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
				try
				{
					Repository.Clone(sourceUrl: gitRepositoryUrl, workdirPath: tagsCloneDirectory,
						options: new CloneOptions { FetchOptions = { CredentialsProvider = ProvidePackageTagCredentials } });
				}
				catch (LibGit2SharpException exception)
				{
					throw new BuildException($"Failed to clone {gitRepositoryUrl} to {tagsCloneDirectory}{GetGitLoginErrorMessage(packageSettings.GitLogin)}: {exception.Message}");
				}
				tagsRepoDirectory = tagsCloneDirectory;
			}

			using var repository = OpenRepository(tagsRepoDirectory);
			foreach (var tagToPush in tagsToPush)
			{
				Console.WriteLine($"Pushing git tag {tagToPush} at {commitSha} (using LibGit2Sharp).");
				repository.ApplyTag(tagName: tagToPush, objectish: commitSha);
				try
				{
					repository.Network.Push(
						remote: repository.Network.Remotes["origin"],
						pushRefSpec: $"refs/tags/{tagToPush}",
						pushOptions: new PushOptions { CredentialsProvider = ProvidePackageTagCredentials });
				}
				catch (LibGit2SharpException exception)
				{
					throw new BuildException($"Failed to push tag {tagToPush} to {gitRepositoryUrl}{GetGitLoginErrorMessage(packageSettings.GitLogin)}: {exception.Message}");
				}
			}

			if (tagsCloneDirectory is not null)
				ForceDeleteDirectory(tagsCloneDirectory);

			Credentials ProvidePackageTagCredentials(string url, string usernameFromUrl, SupportedCredentialTypes types) =>
				new UsernamePasswordCredentials { Username = packageSettings.GitLogin!.Username, Password = packageSettings.GitLogin!.Password };
		}

		static void ForceDeleteDirectory(string path)
		{
			try
			{
				DeleteDirectory(path);
			}
			catch (UnauthorizedAccessException)
			{
				var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };
				foreach (var fileInfo in new DirectoryInfo(path).EnumerateFiles("*", options).Where(x => x.IsReadOnly))
					fileInfo.IsReadOnly = false;
				DeleteDirectory(path);
			}
		}
	}

	/// <summary>
	/// Gets the configuration.
	/// </summary>
	public static string GetConfiguration(this DotNetBuildSettings settings) =>
		settings.BuildOptions!.ConfigurationOption!.Value!;

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
		settings.GetPlatform() is { } platform ? $"-p:Platform={platform}" : null;

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
			null => settings.Verbosity ?? DotNetBuildVerbosity.Minimal,
			_ => throw new BuildException($"Unexpected verbosity option: {settings.BuildOptions.VerbosityOption.Value}"),
		};

	/// <summary>
	/// Gets the argument that specifies the verbosity.
	/// </summary>
	/// <remarks>Defaults to minimal.</remarks>
	public static string GetVerbosityArg(this DotNetBuildSettings settings) =>
		$"-v:{settings.GetVerbosity() switch
		{
			DotNetBuildVerbosity.Quiet => "quiet",
			DotNetBuildVerbosity.Minimal => "minimal",
			DotNetBuildVerbosity.Normal => "normal",
			DotNetBuildVerbosity.Detailed => "detailed",
			DotNetBuildVerbosity.Diagnostic => "diagnostic",
			{ } other => throw new BuildException($"Unexpected DotNetBuildVerbosity: {other}"),
		}}";

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
		settings.GetBuildNumber() is { } buildNumber ? $"-p:BuildNumber={buildNumber}" : null;

	/// <summary>
	/// Gets the argument that specifies whether a build summary should be output.
	/// </summary>
	public static string GetBuildSummaryArg(this DotNetBuildSettings settings) => settings.ShowSummary.GetValueOrDefault() ? "-clp:Summary" : "-clp:NoSummary";

	/// <summary>
	/// Gets extra properties for the specified target.
	/// </summary>
	public static IEnumerable<string> GetExtraPropertyArgs(this DotNetBuildSettings settings, string target)
	{
		if (settings.ExtraProperties?.Invoke(target) is { } pairs)
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
			Parallel.ForEach(paths, settings.RunTests);
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
			var loggerArgs = settings.TestSettings?.GetLogger?.Invoke(path) is { } logger ? new[] { "--logger", logger } : [];
			if (Path.GetExtension(path)?.ToLowerInvariant() is ".dll" or ".exe")
			{
				RunDotNet(new AppRunnerSettings
				{
					Arguments =
					[
						"test",
						Path.GetFileName(path),
						settings.GetVerbosityArg(),
						.. loggerArgs,
						"--",
						"RunConfiguration.TreatNoTestsAsError=true",
					],
					WorkingDirectory = Path.GetDirectoryName(path),
				});
			}
			else
			{
				RunDotNet([
					"test",
					path,
					"-c",
					settings.GetConfiguration(),
					settings.GetPlatformArg(),
					settings.GetBuildNumberArg(),
					"--no-build",
					settings.GetVerbosityArg(),
					settings.GetMaxCpuCountArg(),
					.. settings.GetExtraPropertyArgs("test"),
					.. loggerArgs,
					"--",
					"RunConfiguration.TreatNoTestsAsError=true",
				]);
			}
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

	private static (int Major, int Minor, int Patch, string? Suffix) SplitVersion(string version)
	{
		var hyphenParts = version.Split('-', 2);
		var dotParts = hyphenParts[0].Split('.', 3);
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

	private static string GetGitLoginErrorMessage(GitLoginInfo gitLogin)
	{
		var infos = new List<string>();
		if (gitLogin.Username.Length == 0)
			infos.Add("no username");
		if (gitLogin.Password.Length == 0)
			infos.Add("no password");
		return infos.Count == 0 ? "" : $" ({string.Join(", ", infos)})";
	}

	private readonly struct RuntimeTargetsFile : IDisposable
	{
		public static RuntimeTargetsFile Create(DotNetBuildSettings settings)
		{
			var tempPath = Path.Combine(Path.GetTempPath(), $"FaithlifeBuild.{Guid.NewGuid().ToString("n")[^8..]}.targets");

			var assembly = Assembly.GetExecutingAssembly();
			var resourceName = "Faithlife.Build.Runtime.Directory.Build.targets";
			using var resourceStream = assembly.GetManifestResourceStream(resourceName) ?? throw new BuildException($"Embedded resource '{resourceName}' not found.");
			using var fileStream = File.Create(tempPath);
			resourceStream.CopyTo(fileStream);

			return new(tempPath);
		}

		public RuntimeTargetsFile(string targetsFilePath)
		{
			TargetsFilePath = targetsFilePath;
		}

		public string TargetsFilePath { get; }

		public string GetBuildArg() => $"-p:DirectoryBuildTargetsPath={TargetsFilePath}";

		public void Dispose()
		{
			if (File.Exists(TargetsFilePath))
			{
				try
				{
					File.Delete(TargetsFilePath);
				}
				catch (Exception)
				{
				}
			}
		}
	}
}
