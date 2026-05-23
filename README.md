# Faithlife.Build

**Faithlife.Build** is a build automation system using C# build scripts.

[![Build](https://github.com/Faithlife/FaithlifeBuild/workflows/Build/badge.svg)](https://github.com/Faithlife/FaithlifeBuild/actions?query=workflow%3ABuild) [![NuGet](https://img.shields.io/nuget/v/Faithlife.Build.svg)](https://www.nuget.org/packages/Faithlife.Build)

[Release Notes](./ReleaseNotes.md) | [Contributing](./CONTRIBUTING.md)

## Overview

This library allows developers to use C# to write build scripts. It is similar to [Cake](https://cakebuild.net/) and [NUKE](https://nuke.build/), but simpler, providing a thin wrapper over some of the [libraries acknowledged below](#acknowledgements).

* define [named targets](./src/Faithlife.Build/BuildApp.cs) with [dependencies](./src/Faithlife.Build/BuildTarget.cs), [descriptions](./src/Faithlife.Build/BuildTarget.cs), and [actions](./src/Faithlife.Build/BuildTarget.cs)
* add custom command-line [flags](./src/Faithlife.Build/BuildApp.cs) and [options](./src/Faithlife.Build/BuildApp.cs)
* run [command-line apps](./src/Faithlife.Build/AppRunner.cs) with automatic argument escaping
* run [dotnet commands](./src/Faithlife.Build/DotNetRunner.cs)
* run specific versions of [MSBuild](./src/Faithlife.Build/MSBuildRunner.cs)
* [find files and directories](./src/Faithlife.Build/BuildUtility.cs) using [globs](https://github.com/kthompson/glob/)
* [copy files](./src/Faithlife.Build/BuildUtility.cs) from one directory to another
* get information about the [build environment](./src/Faithlife.Build/BuildEnvironment.cs)
* report [custom build errors](./src/Faithlife.Build/BuildException.cs)
* define [standard targets for .NET builds](#create-net-targets) that build, test, package, and generate documentation for your libraries

Most importantly, since the build script is a full-fledged .NET app with access to any compatible NuGet package, you can do just about anything, in a language and framework you already know well.

## Usage

To use this library for your automated build, create a .NET Console App project in `tools/Build` that references the latest `Faithlife.Build` [NuGet package](https://www.nuget.org/packages/Faithlife.Build). Optionally add the project to your Visual Studio solution file. See [Build.csproj](./tools/Build/Build.csproj) for an example project.

The `Main` method of the console app should call [BuildRunner.Execute](./src/Faithlife.Build/BuildRunner.cs) or [BuildRunner.ExecuteAsync](./src/Faithlife.Build/BuildRunner.cs) with the `args` and a delegate that defines the build targets and any desired command-line options by calling methods on the provided [BuildApp](./src/Faithlife.Build/BuildApp.cs).

```csharp
using Faithlife.Build;
using static Faithlife.Build.DotNetRunner;

internal static class Build
{
	public static int Main(string[] args) => BuildRunner.Execute(args, build =>
	{
		build.Target("build")
			.Describe("Builds the solution")
			.Does(() => RunDotNet("build", "-c", "Release", "--verbosity", "normal"));
	});
}
```

Perform the build by running the `Build` project. This can be done directly via `dotnet run`, e.g. `dotnet run --project tools/Build -- test`, but builds are more easily run from a simple bootstrapper, usually named [build.ps1](./build.ps1), `build.cmd`, and/or `build.sh`.

Specify the desired targets on the command line, e.g. `./build package`. Use `--help` to list the available build targets and command-line options. These command-line arguments are always supported:

* `-n` or `--dry-run` : Don't execute target actions.
* `-s` or `--skip-dependencies` : Don't run any target dependencies.
* `--skip <targets>` : Skip the comma-delimited target dependencies.
* `--parallel` : Run targets in parallel.
* `--no-color` : Disable color output.
* `--show-tree` : Show the dependency tree.
* `--verbose` : Show verbose output.
* `-?` or `-h` or `--help` : Show build help.

Consult the [source code](./src/Faithlife.Build) for additional details.

### Create .NET Targets

To create standard targets for a .NET build, call [DotNetBuild.AddDotNetTargets](./src/Faithlife.Build/DotNetBuild.cs) with [custom settings](./src/Faithlife.Build/DotNetBuildSettings.cs) as needed. See [Build.cs](./tools/Build/Build.cs) for an example.

The supported targets include:

* `clean` : Deletes all build output.
* `restore` : Restores NuGet packages.
* `build` : Builds the solution.
* `test` : Runs the unit tests.
* `coverage` : Runs tests with coverage and generates coverage reports, if coverage settings are configured.
* `package` : Builds NuGet packages.
* `publish` : Publishes NuGet packages and documentation.
* `cleanup` : Runs JetBrains CleanupCode if `jetbrains.resharper.globaltools` is installed as a local or global tool, or if solution settings are present.
* `inspect` : Runs JetBrains InspectCode if `jetbrains.resharper.globaltools` is installed as a local or global tool, or if solution settings are present.

The supported command-line options include:

* `-c <name>` or `--configuration <name>` : The configuration to build (default `Release`).
* `-p <name>` or `--platform <name>` : The solution platform to build.
* `-v <level>` or `--verbosity <level>` : The build verbosity (`q[uiet]`, `m[inimal]`, `n[ormal]`, `d[etailed]`).
* `--version-suffix <suffix>` : Generates a prerelease NuGet package.
* `--nuget-output <path>` : Directory for the generated NuGet package (default `release`).
* `--trigger <name>` : The git branch or tag that triggered the build.
* `--build-number <number>` : The automated build number.
* `--no-test` : Skip the unit tests.

The supported [DotNetBuildSettings](./src/Faithlife.Build/DotNetBuildSettings.cs) include:

* `SolutionName` : The name of the solution file; defaults to the only solution in the directory.
* `SolutionPlatform` : The default solution platform to build.
* `Verbosity` : The default build verbosity.
* `NuGetApiKey` : The NuGet API key with which to push packages.
* `NuGetSource` : The NuGet source to which to push packages, if not the standard source.
* `DocsSettings` : Used to generate Markdown documentation from XML comments.
* `MSBuildSettings` : Set to use `MSBuild` instead of `dotnet` to build the solution.
* `TestSettings` : Settings for running unit tests.
* `CoverageSettings` : Settings for running unit tests with coverage.
* `PackageSettings` : Settings for creating and publishing NuGet packages.
* `CleanSettings` : Settings for cleaning projects.
* `MaxCpuCount` : The maximum number of CPUs to use when building.
* `ExtraProperties` : A function that returns any extra properties for the specified build target.
* `ShowSummary` : True if a build summary should be displayed.
* `BuildNumber` : The build number, if not specified on the command line.

For details on exactly what each target does and how the settings and command-line options affect the build, read the [DotNetBuild source code](./src/Faithlife.Build/DotNetBuild.cs).

### Automatic MSBuild Properties

When using `DotNetBuild.AddDotNetTargets`, `Faithlife.Build` will set the following MSBuild properties automatically; you should not set them in `Project.csproj` or `Directory.Build.props`:

* `AllowedOutputExtensionsInPackageBuildOutputFolder`: set to include `.pdb` files.
* `AssemblyVersion`: set to `$(VersionPrefix).$(BuildNumber)` if `BuildNumber` is provided.
* `ContinuousIntegrationBuild`: set to `true` if a CI environment is detected.
* `PublishRepositoryUrl`: set to `true` to include repository URL in NuGet package metadata.

Additionally, this property and item come from the .NET SDK and should not be set manually:

* `EmbedUntrackedFiles`: set to `true`.
* `SourceLinkGitHubHost`: `ContentUrl="https://raw.githubusercontent.com"` is added for GitHub.com repositories.

To check the exact MSBuild properties and items being used, read the [Runtime.Directory.Build.targets source code](./src/Faithlife.Build/Runtime.Directory.Build.targets).

## Coverage

Set `DotNetBuildSettings.CoverageSettings` to add a standard `coverage` target that runs test projects under `tests/**/*.csproj` with Coverlet and generates reports with `dotnet dnx dotnet-reportgenerator-globaltool`.

```csharp
build.AddDotNetTargets(new DotNetBuildSettings
{
	CoverageSettings = new DotNetCoverageSettings
	{
		TargetFramework = "net10.0",
		AssemblyFilters = ["+MyProject*", "-*.Tests"],
	},
});
```

The target writes test results under `artifacts/Coverage/TestResults`, writes reports to `artifacts/Coverage/Report`, and uses `coverage.runsettings` automatically when that file exists. Each run uses a fresh test-results subdirectory so stale coverage files are not included, but the target does not delete configured coverage directories.

## Acknowledgements

Special thanks to the libraries and tools used by `Faithlife.Build`:

* [Bullseye](https://github.com/adamralph/bullseye)
* [Glob](https://github.com/kthompson/glob/)
* [LibGit2Sharp](https://github.com/libgit2/libgit2sharp/)
* [McMaster.Extensions.CommandLineUtils](https://github.com/natemcmaster/CommandLineUtils)
* [Polly](https://github.com/App-vNext/Polly)
* [SimpleExec](https://github.com/adamralph/simple-exec)
* [XmlDocMarkdown](http://ejball.com/XmlDocMarkdown/)

Also thanks to Paul Knopf for [this article](https://pknopf.com/post/2019-03-10-you-dont-need-cake-anymore-the-way-to-build-dotnet-projects-going-forward/), which inspired us to think beyond Cake.
