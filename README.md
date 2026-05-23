# Faithlife.Build

A build automation system using C# build scripts.

[![Build](https://github.com/Faithlife/FaithlifeBuild/workflows/Build/badge.svg)](https://github.com/Faithlife/FaithlifeBuild/actions?query=workflow%3ABuild) [![NuGet](https://img.shields.io/nuget/v/Faithlife.Build.svg)](https://www.nuget.org/packages/Faithlife.Build)

[Documentation](https://faithlife.github.io/FaithlifeBuild/) | [Release Notes](https://github.com/Faithlife/FaithlifeBuild/blob/master/ReleaseNotes.md) | [Contributing](https://github.com/Faithlife/FaithlifeBuild/blob/master/CONTRIBUTING.md)

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
