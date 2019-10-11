# Faithlife.Build

**Faithlife.Build** is a build automation system using C# build scripts.

## Overview

This library allows developers to use C# to write build scripts. It is similar to [Cake](https://cakebuild.net/) and [NUKE](https://nuke.build/), but simpler, providing a thin wrapper over some of the [libraries acknowledged below](#acknowledgements).

* define [named targets](Faithlife.Build/BuildApp/Target.md) with [dependencies](Faithlife.Build/BuildTarget/DependsOn.md), [descriptions](Faithlife.Build/BuildTarget/Describe.md), and [actions](Faithlife.Build/BuildTarget/Does.md)
* add custom command-line [flags](Faithlife.Build/BuildApp/AddFlag.md) and [options](Faithlife.Build/BuildApp/AddOption.md)
* run [command-line apps](Faithlife.Build/AppRunner.md) with automatic argument escaping
* run [.NET Core Global Tools](Faithlife.Build/DotNetTools.md) and/or [classic NuGet packaged tools](Faithlife.Build/DotNetTools.md)
* run [dotnet commands](Faithlife.Build/DotNetRunner.md) and/or specific versions of [MSBuild](Faithlife.Build/MSBuildRunner.md)
* [find files and directories](Faithlife.Build/BuildUtility.md) using [globs](https://github.com/kthompson/glob/)
* [copy files](Faithlife.Build/BuildUtility/CopyFiles.md) from one directory to another
* get information about the [build environment](Faithlife.Build/BuildEnvironment.md)
* define [standard targets for .NET builds](#net-targets) that build, test, package, and generate documentation for your libraries
* `--help` displays supported command-line options and targets with descriptions

## Usage

To use this library for your automated build, create a .NET Core Console App project in `tools/build` that references the latest `Faithlife.Build` [NuGet package](https://www.nuget.org/packages/Faithlife.Build). Optionally add the project to your Visual Studio solution file. See [`Build.csproj`](https://github.com/Faithlife/FaithlifeBuild/blob/master/tools/Build/Build.csproj) for an example project; there are project properties in that file that you may need for everything to work as expected.

The `Main` method of the console app should call [`BuildRunner.Execute`](Faithlife.Build/BuildRunner/Execute.md) with the `args` and a delegate that defines the build targets and any desired command-line options by calling methods on the provided [`BuildApp`](Faithlife.Build/BuildApp.md).

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

Perform the build by calling `dotnet run` on the build project, which is most easily done from a simple bootstrapper, typically named [`build.cmd`](https://github.com/Faithlife/FaithlifeBuild/blob/master/build.cmd) (for Windows) and/or [`build.sh`](https://github.com/Faithlife/FaithlifeBuild/blob/master/build.sh) (for non-Windows).

Consult the full [reference documentation](Faithlife.Build.md) for additional details.

### Create .NET Targets

To create standard targets for a .NET build, call [`DotNetBuild.AddDotNetTargets`](Faithlife.Build/DotNetBuild/AddDotNetTargets.md) with [custom settings](Faithlife.Build/DotNetBuildSettings.md) as needed. See [`Build.cs`](https://github.com/Faithlife/FaithlifeBuild/blob/master/tools/Build/Build.cs) for an example.

The supported targets include:

* `clean` : Deletes all build output.
* `restore` : Restores NuGet packages.
* `build` : Builds the solution.
* `test` : Runs the unit tests.
* `package` : Builds NuGet packages.
* `publish` : Publishes NuGet packages and documentation.

The supported command-line options include:

* `--configuration <name>` : The configuration to build (default `Release`).
* `--platform <name>` : The solution platform to build.
* `--version-suffix <suffix>` : Generates a prerelease NuGet package.
* `--nuget-output <path>` : Directory for the generated NuGet package (default `release`).
* `--trigger <name>` : The git branch or tag that triggered the build.
* `--build-number <number>` : The automated build number.
* `--no-test` : Skip the unit tests.

The supported [`DotNetBuildSettings`](Faithlife.Build/DotNetBuildSettings.md) include:

* `SolutionName` : The name of the solution file; defaults to the only solution in the directory.
* `SolutionPlatform` : The default solution platform to build.
* `NuGetSource` : The NuGet source to which to push packages, if not the standard source.
* `DocsSettings` : Used to generate Markdown documentation from XML comments.
* `MSBuildSettings` : Set to use `MSBuild` instead of `dotnet` to build the solution.

For details on exactly what each target does and how the settings and command-line options affect the build, read the [source code](https://github.com/Faithlife/FaithlifeBuild/blob/master/src/Faithlife.Build/DotNetBuild.cs).

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
