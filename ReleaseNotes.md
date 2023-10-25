# Release Notes

## 5.19.1

* Never publish NuGet package with version `0.0.0`.

## 5.19.0

* Allow global JetBrains tool to be used. (`dotnet tool update --global jetbrains.resharper.globaltools`)
* Drop support for `format` target. (The `dotnet-format` tool is no longer being updated. Just use `dotnet format` directly.)

## 5.18.1

* Update dependencies to fix [CVE-2023-29337](https://github.com/advisories/GHSA-6qmf-mmc7-6c2p).

## 5.18.0

* Support floating version numbers of NuGet packages in `DotNetClassic` projects inside `Build.csproj`.

## 5.17.2

* Add README to package.

## 5.17.1

* Clone docs into temp directory when branch is specified.

## 5.17.0

* Add `publish-nuget-output` trigger for use with the `publish` target.
  * Can be used to publish everything in `--nuget-output` without rebuilding packages. e.g. `publish --skip package --trigger publish-nuget-output`.
* Always use `--skip-duplicates` when pushing to NuGet.

## 5.16.1

* Fix syntax error in verbosity parameter passed to `dotnet`.

## 5.16.0

* Remove netstandard2.x targets: [#42](https://github.com/Faithlife/FaithlifeBuild/issues/42).
* Faithlife.Build now only targets `net6.0`.
* Docs are cloned from the repo for the local working copy if possible.
* Update Bullseye to 4.2.0.

## 5.15.0

* Push git tags using the GitHub API: [#43](https://github.com/Faithlife/FaithlifeBuild/pull/43).
  * This requires the `GITHUB_API_URL` environment variable to be present and specify the base address of the GitHub API.
  * This environment variable is automatically supplied to GitHub Actions runners.
* Add `--parallel` and `--verbose` flags: [#40](https://github.com/Faithlife/FaithlifeBuild/pull/40).
* Update NuGet dependencies to address CVE-2022-41032.

## 5.14.7

* Use new `--build` option for InspectCode.
* Update LibGit2Sharp.

## 5.14.5–5.14.6

* Improve exception message for LibGit2Sharp errors.

## 5.14.4

* Allow current directory to be a subdirectory of the git repository.

## 5.14.3

* Send `BuildNumber` property for all dotnet/msbuild commands.

## 5.14.2

* Make implicit usings transitive.

## 5.14.1

* Use `dotnet` if the full path can't be determined.

## 5.14.0

* Treat no tests as error.
* Add [implicit usings](./src/Faithlife.Build/Faithlife.Build.props) for consumers.

## 5.13.1

* Support Visual Studio 2022 Build Tools.

## 5.13.0

* Support Visual Studio 2022.

## 5.12.0

* Support multiple target frameworks for `XmlDocGen`.

## 5.11.5

* Use standard temp path when cloning repositories to avoid path-too-long issues with `LibGit2Sharp`.

## 5.11.4

* Use latest `Bullseye`.

## 5.11.3

* Use latest `LibGit2Sharp` for issue with latest .NET 5 SDK.

## 5.11.0–5.11.2

* Support running multiple test projects in parallel.

## 5.10.3

* Avoid symlinks when recursively scanning cloned directory, in case of symlink cycle.

## 5.10.2

* Don't return from running an app before all output/error lines are read.

## 5.10.1

* Don't publish documentation until the NuGet package is published.

## 5.10.0

* Support capturing application output.
* Change application echo formatting.
* SimpleExec is no longer used.
* Support pushing git tag on successful publish of NuGet package.

## 5.9.3

* Extra properties aren't allowed when testing an assembly.

## 5.9.2

* Fix crash when running tests.

## 5.9.1

* `CopyFiles` and `CopyFilesExcept` should overwrite existing files.

## 5.9.0

* Fail if entry point returns void.

## 5.9.0-beta.2

* Expose `RunTests` extension method on `DotNetBuildSettings`.

## 5.9.0-beta.1

* Support `XmlDocGen` as .NET Framework app.

## 5.8.0

* Add `BuildNumber` to `DotNetBuildSettings`.
* Support build number environment variable used by Jenkins.

## 5.7.0

* Replace `FindTestAssemblies` with `FindAssemblies` in `DotNetTestSettings`.
* Make `DotNetBuildSettings` helper methods extension methods.

## 5.6.0

* Suppress .NET build summary of warnings and errors. (Use `DotNetBuildSettings.ShowSummary` to restore it.)
* Update `Bullseye` and `McMaster.Extensions.CommandLineUtils`.

## 5.5.0

* Don't generate documentation for prerelease packages.
* Don't unnecessarily 'clean' before 'package'.
* Add `DeleteDirectory` to `BuildUtility`.
* Add static methods to `DotNetBuild` to help with standard build options.
* Improve exception handling during build script initialization.

## 5.4.1

* Fix bug when using `XmlDocGen`.

## 5.4.0

* Support async target actions.
* Support `DotNetClassicTool` for running tools directly from restored NuGet packages referenced in `Build.csproj`.
* Obsolete `DotNetTools`.
* Use `dotnet test` instead of `dotnet vstest`.
* Support `FindAssemblies` with `XmlDocGen` strategy.
* Use latest libraries.

## 5.3.1

* Allow target `publish` to work if `package` is skipped.

## 5.3.0

* Support `--skip <targets>`.

## 5.2.2

* Fix bug when calling `DotNetLocalTool.Any`.

## 5.2.1

* Fix bug when looking for `dotnet-tools.json`.

## 5.2.0

* Support `XmlDocGen` for generating docs.
* Refuse to auto-publish NuGet package with version `0.0.0`.
* Use shorter temp path for NuGet packages.
* Write actual NuGet package path to console.

## 5.1.0

* Add and use `DotNetLocalTool.(Try)Create(From)` and `Any(From)`.
* Add `IsFrameworkApp` and `UseCmdOnWindows` to `AppRunnerSettings`. Obsolete `RunDotNetFrameworkApp` and `RunCmd`.
* Use configuration and platform when running JetBrains.

## 5.0.0

* Default to minimal verbosity.
* The `publish` target publishes packages and documentation if no `--trigger` is specified.
* The `restore` target restores local .NET tools via `dotnet tool restore`.
* Use locally installed `xmldocmd` to publish documentation.
* Detect current git branch from `GITHUB_REF` in addition to `APPVEYOR_REPO_BRANCH`.
* Get build number from `APPVEYOR_BUILD_NUMBER` or `GITHUB_RUN_NUMBER`.
* Improve exit code exception.
* Clean all `tools` projects but keep `tools/bin`.
* Support `--verbosity` command-line option.
* Add target `format` if `dotnet-format` is installed locally.
* Add targets `cleanup` and `inspect` if `JetBrains.ReSharper.GlobalTools` is installed locally.
* Don't show callstack for `InvalidUsageException`.

## 4.8.0

* Don't show call stack for `BuildException`.

## 4.7.1

* Add `DotNetBuildSettings.Verbosity`.

## 4.6.1

* Skip duplicate NuGet packages when using `--trigger publish-all`.

## 4.6.0

* Support `--dry-run` and `--show-tree`.

## 4.5.1

* Fix NuGet.CommandLine version path.

## 4.5.0

* Add `MSBuildSettings.MSBuildPath`.

## 4.4.1

* Catch command-line exception at the proper location.
* Use embedded debugging symbols.

## 4.4.0

* Show build target summary.
* Upgrade Bullseye and other dependencies.

## 4.3.0

* Add method for copying all files that don't match the provided globbing patterns.

## 4.2.0

* Support `--skip-dependencies` (`-s`).

## 4.1.0

* `AppRunner.RunCmd` uses `cmd /c` on Windows.

## 4.0.0

* Stop setting the working directory to the grandparent of the script directory. The working directory is now left as-is. Bootstrapper scripts should set the current directory if desired. (Removing this feature makes build scripts more flexible. It also prevents the build from running in the wrong directory if the script is moved with its compiled output.)

## 3.12.0

* Support finding existing flags and options.

## 3.11.0

* Allow clearing actions on a BuildTarget.

## 3.10.2

* Clean `XmlDocTarget`.

## 3.10.1

* Try to determine branch for publishing documentation.

## 3.10.0

* Allow extra information after publish triggers.

## 3.9.0

* Don't push packages already pushed when detecting triggers via tags.
* Drop support for `sourcelink test`. Too many bugs and not actively developed.

## 3.8.0

* Support C# 8 nullable references.
* Support .NET local tools.

## 3.7.4

* Fix Visual Studio 2019 path.

## 3.7.3

* Update `xmldocmd`.
* Improve clarity of publish instruction.

## 3.7.2

* Retry deleting directory.

## 3.7.1

* Ensure directory exists before deleting it, in case its ancestor directory was already deleted.

## 3.7.0

* Support `DotNetDocsSettings.ProjectHasDocs`.

## 3.6.1–3.6.2

* Update `xmldocmd`.

## 3.6.0

* Run nuget.exe in non-interactive mode.
* Allow customization of how tests are run.

## 3.5.0

* Use new `DotNetTools.GetClassicToolPath` instead of now-obsolete `PackagedTools`.

## 3.4.0

* Support customizing directories to delete when cleaning.

## 3.3.0

* Support finding projects to test and/or package.

## 3.2.0

* Trigger detection should support `publish-` tags.
* Update `xmldocmd`.
* Support `DotNetDocsSettings.FindAssemblies`.

## 3.1.0

* Use SimpleExec again.
* Don't clean `release` and `tools/bin`.
* Add `--no-test` flag for skipping unit tests.
* Support `--trigger=detect` for build systems that can't trigger builds on new tags.

## 3.0.0

* Drop support for `packagediff` for now. (Too buggy.)
* `package` target depends on `clean` for simpler package detection.
* Support SourceLink authentication, moving settings into `SourceLinkSettings`. Now disabled by default.

## 2.2.0

* Add `DotNetBuildProperties.ExtraProperties`.
* Delete package if it already exists.

## 2.1.4

* Delete files before running `dotnet clean`.

## 2.1.3

* Don't use the "pager" for help.

## 2.1.2

* `dotnet push` doesn't support `-maxcpucount`.

## 2.1.1

* `dotnet restore` needs platform to work correctly.

## 2.1.0

* Support `DotNetBuildSettings.MaxCpuCount`.
* Run `dotnet clean` or equivalent when cleaning solution.

## 2.0.3

* Upgrade `sourcelink`.

## 2.0.2

* Don't fail build if packagediff crashes.

## 2.0.1

* Don't fail build on bad package version until publishing.

## 2.0.0

* Support setting the automated build number.
* Return exit code from app runners.
* Use [`packagediff`](https://www.nuget.org/packages/Faithlife.PackageDiffTool.Tool) to check semantic versioning.

## 1.3.0

* Support setting the working directory when running apps.
* Run `dotnet vstest` from the output directory (for Mac).

## 1.2.0

* Support finding test assemblies explicitly.
* Support cloning the settings.
* Restore packages using the correct configuration and platform when using MSBuild.

## 1.1.0

* Support specifying solution platform (via setting and/or command-line option).

## 1.0.2

* MSBuild is not a .NET Framework app.

## 1.0.1

* Try to find MSBuild on macOS and Linux.

## 1.0.0

* Wrap MSBuildVersion and MSBuildPlatform into MSBuildSettings.

## 0.10.0

* Support `XmlDocTarget` for documentation generation.
* Support building with MSBuild.

## 0.9.0

* Support additional publish triggers for .NET builds.
* Support `--no-color` option.

## 0.8.0

* Support custom sources for `DotNetTools`.

## 0.7.1

* Restore doesn't support configuration.

## 0.7.0

* Update XmlDocMarkdown.
* Add restore target to DotNetBuild.

## 0.6.0

* Add `FindFilesFrom` and `FindDirectoriesFrom` to `BuildUtility`.
* Globs should use case-insensitive matching.
* Don't return the same found file/directory twice.
* `BuildUtility.CopyFiles` uses globs to copy files.
* Run MSBuild via `MSBuildRunner`.
* Support running tools from NuGet packages via `PackagedTools`.
* Support `IEnumerable<string> args`.
* Handle `InvalidUsageException`.

## 0.5.0

* Don't document default target unless it has a description.
* Add `DotNetBuildSettings.ProjectUsesSourceLink`.
* Update XmlDocMarkdown.

## 0.4.1

* Make private method public.

## 0.4.0

* Use `DotNetBuildOptions` to access and/or override build options.
* Automatically run any target named `default`.
* Return existing target; allow multiple actions per target.
* Don't crash if a packaged project doesn't output a DLL.
* Support `update-docs` publish trigger for .NET builds.
* Add `BuildEnvironment` and .NET Framework app support.

## 0.3.1

* Support publishing multiple NuGet packages.
* Catch exceptions from Bullseye.

## 0.3.0

* Support `--nuget-output` with .NET builds.
* Use the `xmldocmd` .NET Core Global Tool to generate documentation.
* Use fixed but customizable versions of .NET Core Global Tools for stable builds.

## 0.2.0

* Use `DotNetTools` to run .NET Core tools.
* Change settings for documentation generation.
* Generate documentation in `docs` folder instead of `gh_pages` branch.

## 0.1.1

* Upgrade dependencies (especially Glob, which now targets `netstandard2.0`).

## 0.1.0

* Initial release.
