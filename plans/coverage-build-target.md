# Proposal: Standard Coverage Build Target

## Context

MuchAdo currently defines a custom `coverage` target in `tools/Build/Build.cs`. That target depends on `build`, cleans `artifacts/Coverage/TestResults` and `artifacts/Coverage/Report`, runs every test project with Coverlet (`--collect "XPlat Code Coverage"`) and `coverage.runsettings`, invokes `dotnet dnx dotnet-reportgenerator-globaltool`, writes the text summary to the console, appends the markdown assemblies summary to `GITHUB_STEP_SUMMARY`, and leaves the HTML/Cobertura reports under `artifacts/Coverage/Report`.

FaithlifeBuild already provides standard `clean`, `restore`, `build`, `test`, `package`, `publish`, and related .NET build behavior through `DotNetBuild.AddDotNetTargets`. It has `DotNetTestSettings` for customizing test discovery and execution, but no first-class coverage settings or `coverage` target.

## Proposal

Add an opt-in standard `coverage` target to `DotNetBuild.AddDotNetTargets` when coverage settings are provided:

```csharp
var settings = new DotNetBuildSettings
{
    CoverageSettings = new DotNetCoverageSettings
    {
        TargetFramework = "net10.0",
        AssemblyFilters = ["+MuchAdo*", "-*.Tests"],
    },
};
```

When `CoverageSettings` is `null`, FaithlifeBuild should not register a `coverage` target. This keeps existing generated build scripts and consumers unchanged unless they intentionally opt in.

## Proposed API

Add `DotNetCoverageSettings` with narrowly scoped defaults that match the MuchAdo pattern but remain reusable:

```csharp
public sealed class DotNetCoverageSettings
{
    public Func<DotNetBuildSettings, IReadOnlyList<string>>? FindProjects { get; set; }
    public string? TargetFramework { get; set; }
    public string? RunSettingsPath { get; set; }
    public string? TestResultsDirectory { get; set; }
    public string? ReportDirectory { get; set; }
    public IReadOnlyList<string>? AssemblyFilters { get; set; }
    public IReadOnlyList<string>? ReportTypes { get; set; }
    public DotNetCoverageSettings Clone() => (DotNetCoverageSettings) MemberwiseClone();
}
```

Add `DotNetBuildSettings.CoverageSettings` and clone it in `DotNetBuildSettings.Clone`.

Recommended behavior:

* Default project discovery: use `CoverageSettings.FindProjects` when provided; otherwise use `FindFiles("tests/**/*.csproj")`.
* Do not support assembly paths for coverage; coverage should stay project-oriented because `dotnet test --collect` is the normal Coverlet path.
* Default `RunSettingsPath` just in time to `coverage.runsettings` when that file exists; omit `--settings` when neither `RunSettingsPath` nor `coverage.runsettings` exists.
* Default `TestResultsDirectory` just in time to `artifacts/Coverage/TestResults`.
* Default `ReportDirectory` just in time to `artifacts/Coverage/Report`.
* Default report types just in time to `Html;Cobertura;TextSummary;MarkdownAssembliesSummary`.
* Default logger per project: `trx;LogFileName={Path.GetFileNameWithoutExtension(project)}.coverage.trx`.
* Include `--framework <TargetFramework>` only when `TargetFramework` is set.
* Include `--settings <RunSettingsPath>` only when `RunSettingsPath` is set.
* Pass `settings.GetExtraPropertyArgs("coverage")` to test invocations so consumers can customize MSBuild properties for coverage runs.
* Preserve `RunConfiguration.TreatNoTestsAsError=true`, matching the standard `test` target.

Do not add a `--no-coverage` flag. Omitting `CoverageSettings` is enough to avoid registering or running coverage behavior.

Do not add `GetLogger` for now. The standard coverage target should use the fixed per-project coverage TRX logger above until a concrete consumer needs customization.

Do not add `AfterReportGenerated`. That idea was only a speculative extension point for custom post-processing; the known requirements are covered by printing the text summary, appending the GitHub summary, and uploading `artifacts/Coverage/Report` from CI.

## Target Behavior

Register the target inside `AddDotNetTargets` after `test` and before `package`:

```csharp
if (settings.CoverageSettings is not null)
{
    build.Target("coverage")
        .DependsOn("build")
        .Describe("Runs tests with coverage and generates coverage reports")
        .Does(() => settings.RunCoverage());
}
```

`package` should continue to depend on `test`, not `coverage`. Coverage is a reporting workflow, not a packaging prerequisite.

The implementation should factor the work into reusable helpers on `DotNetBuild`, for example:

* `RunCoverage(this DotNetBuildSettings settings)`
* `FindCoverageProjects(this DotNetBuildSettings settings)`
* `GetCoverageRunSettingsPath(DotNetCoverageSettings settings)`
* `CleanCoverageDirectory(string path)` or a private helper inside `RunCoverage`
* `WriteCoverageSummary(DotNetCoverageSettings settings)`

The core command sequence should mirror MuchAdo:

```text
dotnet test <project> -c <configuration> <platform> <build-number> --no-build <verbosity> <maxcpucount> <extra coverage properties> --framework <target framework> --results-directory <coverage test results> --logger <coverage logger> --collect "XPlat Code Coverage" --settings <runsettings> -- RunConfiguration.TreatNoTestsAsError=true

dotnet dnx dotnet-reportgenerator-globaltool --yes -reports:<coverage test results>/*/coverage.cobertura.xml -targetdir:<coverage report directory> -reporttypes:<report types> -assemblyfilters:<filters>
```

After ReportGenerator completes, FaithlifeBuild should:

* Print `Summary.txt` when it exists.
* Append `Summary.md` to `GITHUB_STEP_SUMMARY` when both exist.
* Print the absolute coverage report path.

## Consumer Example: MuchAdo

After FaithlifeBuild ships the target, MuchAdo could replace its custom `coverage` target with settings:

```csharp
var buildSettings = new DotNetBuildSettings
{
    NuGetApiKey = Environment.GetEnvironmentVariable("NUGET_API_KEY"),
    PackageSettings = new DotNetPackageSettings { PushTagOnPublish = x => $"v{x.Version}" },
    CoverageSettings = new DotNetCoverageSettings
    {
        TargetFramework = "net10.0",
        AssemblyFilters = ["+MuchAdo*", "-*.Tests"],
    },
};
```

Repos with Docker-specific test subsets can still keep custom targets such as MuchAdo's `test-no-docker` and `test-docker`. The standard `coverage` target would handle the common "all coverage-capable test projects" case.

## Implementation Steps

* Add `DotNetCoverageSettings` in `src/Faithlife.Build`.
* Add `CoverageSettings` to `DotNetBuildSettings` and clone it.
* Register `coverage` in `DotNetBuild.AddDotNetTargets` only when `CoverageSettings` is provided.
* Implement coverage project discovery, just-in-time coverage defaults, directory cleanup, `dotnet test` coverage execution, ReportGenerator execution, and summary output helpers.
* Update XML comments for `DotNetBuildSettings.ExtraProperties` to list `coverage` as a supported target.
* Add README or generated docs coverage notes showing the minimal opt-in settings.
* Add release notes once the implementation is ready.

## Test Plan

* Unit test that `AddDotNetTargets` does not list `coverage` when `CoverageSettings` is `null`.
* Unit test that `AddDotNetTargets` lists `coverage` with the expected description when `CoverageSettings` is set.
* Unit test `DotNetBuildSettings.Clone` copies `CoverageSettings`.
* Unit test that `RunSettingsPath` defaults to `coverage.runsettings` when that file exists and is omitted otherwise.
* Add command-construction coverage if the test harness is extended to intercept `RunDotNet` calls; otherwise keep command behavior covered by a small sample consumer or integration-style build script test.
* Manually validate with a consumer such as MuchAdo: run `./build.ps1 coverage --skip build`, confirm test results under `artifacts/Coverage/TestResults`, reports under `artifacts/Coverage/Report`, console summary output, and markdown assemblies summary output in CI.

## Settled Decisions

* Coverage project discovery should default to `FindFiles("tests/**/*.csproj")`.
* ReportGenerator should be invoked via `dotnet dnx dotnet-reportgenerator-globaltool`, matching MuchAdo.
* There should be no `--no-coverage` flag.
* `CoverageSettings` should not support assembly paths.
* `RunSettingsPath` should default to `coverage.runsettings` when that file exists.
* `TestResultsDirectory` and `ReportDirectory` should be nullable settings with defaults applied just in time.
* `GetLogger` and `AfterReportGenerated` should not be part of the initial API.
