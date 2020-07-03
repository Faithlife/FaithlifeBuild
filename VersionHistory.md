# Version History

### 5.0.0

* Default to minimal verbosity.
* The `publish` target publishes packages and documentation if no `--trigger` is specified.
* The `restore` target restores local .NET tools via `dotnet tool restore`.
* Now requires locally installed `xmldocmd` to publish documentation.
* Detect current git branch from `GITHUB_REF` in addition to `APPVEYOR_REPO_BRANCH`.
* Get build number from `APPVEYOR_BUILD_NUMBER` or `GITHUB_RUN_NUMBER`.
* Improve exit code exception.
* Clean all `tools` projects but keep `tools/bin`.

### 4.8.0

* Don't show call stack for `BuildException`.

### 4.7.1

* Add `DotNetBuildSettings.Verbosity`.

### 4.6.1

* Skip duplicate NuGet packages when using `--trigger publish-all`.

### 4.6.0

* Support `--dry-run` and `--show-tree`.

### 4.5.1

* Fix NuGet.CommandLine version path.

### 4.5.0

* Add `MSBuildSettings.MSBuildPath`.

### 4.4.1

* Catch command-line exception at the proper location.
* Use embedded debugging symbols.

### 4.4.0

* Show build target summary.
* Upgrade Bullseye and other dependencies.

### 4.3.0

* Add method for copying all files that don't match the provided globbing patterns.

### 4.2.0

* Support `--skip-dependencies` (`-s`).

### 4.1.0

* `AppRunner.RunCmd` uses `cmd /c` on Windows.

### 4.0.0

* Stop setting the working directory to the grandparent of the script directory. The working directory is now left as-is. Bootstrapper scripts should set the current directory if desired. (Removing this feature makes build scripts more flexible. It also prevents the build from running in the wrong directory if the script is moved with its compiled output.)

### 3.12.0

* Support finding existing flags and options.

### 3.11.0

* Allow clearing actions on a BuildTarget.

### 3.10.2

* Clean `XmlDocTarget`.

### 3.10.1

* Try to determine branch for publishing documentation.

### 3.10.0

* Allow extra information after publish triggers.

### 3.9.0

* Don't push packages already pushed when detecting triggers via tags.
* Drop support for `sourcelink test`. Too many bugs and not actively developed.

### 3.8.0

* Support C# 8 nullable references.
* Support .NET local tools.

### 3.7.4

* Fix Visual Studio 2019 path.

### 3.7.3

* Update `xmldocmd`.
* Improve clarity of publish instruction.

### 3.7.2

* Retry deleting directory.

### 3.7.1

* Ensure directory exists before deleting it, in case its ancestor directory was already deleted.

### 3.7.0

* Support `DotNetDocsSettings.ProjectHasDocs`.

### 3.6.1–3.6.2

* Update `xmldocmd`.

### 3.6.0

* Run nuget.exe in non-interactive mode.
* Allow customization of how tests are run.

### 3.5.0

* Use new `DotNetTools.GetClassicToolPath` instead of now-obsolete `PackagedTools`. 

### 3.4.0

* Support customizing directories to delete when cleaning.

### 3.3.0

* Support finding projects to test and/or package.

### 3.2.0

* Trigger detection should support `publish-` tags.
* Update `xmldocmd`.
* Support `DotNetDocsSettings.FindAssemblies`.

### 3.1.0

* Use SimpleExec again.
* Don't clean `release` and `tools/bin`.
* Add `--no-test` flag for skipping unit tests.
* Support `--trigger=detect` for build systems that can't trigger builds on new tags.

### 3.0.0

* Drop support for `packagediff` for now. (Too buggy.)
* `package` target depends on `clean` for simpler package detection.
* Support SourceLink authentication, moving settings into `SourceLinkSettings`. Now disabled by default.

### 2.2.0

* Add `DotNetBuildProperties.ExtraProperties`.
* Delete package if it already exists.

### 2.1.4

* Delete files before running `dotnet clean`.

### 2.1.3

* Don't use the "pager" for help.

### 2.1.2

* `dotnet push` doesn't support `-maxcpucount`.

### 2.1.1

* `dotnet restore` needs platform to work correctly.

### 2.1.0

* Support `DotNetBuildSettings.MaxCpuCount`.
* Run `dotnet clean` or equivalent when cleaning solution.

### 2.0.3

* Upgrade `sourcelink`.

### 2.0.2

* Don't fail build if packagediff crashes.

### 2.0.1

* Don't fail build on bad package version until publishing.

### 2.0.0

* Support setting the automated build number.
* Return exit code from app runners.
* Use [`packagediff`](https://www.nuget.org/packages/Faithlife.PackageDiffTool.Tool) to check semantic versioning.

### 1.3.0

* Support setting the working directory when running apps.
* Run `dotnet vstest` from the output directory (for Mac).

### 1.2.0

* Support finding test assemblies explicitly.
* Support cloning the settings.
* Restore packages using the correct configuration and platform when using MSBuild.

### 1.1.0

* Support specifying solution platform (via setting and/or command-line option).

### 1.0.2

* MSBuild is not a .NET Framework app.

### 1.0.1

* Try to find MSBuild on macOS and Linux.

### 1.0.0

* Wrap MSBuildVersion and MSBuildPlatform into MSBuildSettings.

### 0.10.0

* Support `XmlDocTarget` for documentation generation.
* Support building with MSBuild.

### 0.9.0

* Support additional publish triggers for .NET builds.
* Support `--no-color` option.

### 0.8.0

* Support custom sources for `DotNetTools`.

### 0.7.1

* Restore doesn't support configuration.

### 0.7.0

* Update XmlDocMarkdown.
* Add restore target to DotNetBuild.

### 0.6.0

* Add `FindFilesFrom` and `FindDirectoriesFrom` to `BuildUtility`.
* Globs should use case-insensitive matching.
* Don't return the same found file/directory twice.
* `BuildUtility.CopyFiles` uses globs to copy files.
* Run MSBuild via `MSBuildRunner`.
* Support running tools from NuGet packages via `PackagedTools`.
* Support `IEnumerable<string> args`.
* Handle `InvalidUsageException`.

### 0.5.0

* Don't document default target unless it has a description.
* Add `DotNetBuildSettings.ProjectUsesSourceLink`.
* Update XmlDocMarkdown.

### 0.4.1

* Make private method public.

### 0.4.0

* Use `DotNetBuildOptions` to access and/or override build options.
* Automatically run any target named `default`.
* Return existing target; allow multiple actions per target.
* Don't crash if a packaged project doesn't output a DLL.
* Support `update-docs` publish trigger for .NET builds.
* Add `BuildEnvironment` and .NET Framework app support.

### 0.3.1

* Support publishing multiple NuGet packages.
* Catch exceptions from Bullseye.

### 0.3.0

* Support `--nuget-output` with .NET builds.
* Use the `xmldocmd` .NET Core Global Tool to generate documentation.
* Use fixed but customizable versions of .NET Core Global Tools for stable builds.

### 0.2.0

* Use `DotNetTools` to run .NET Core tools.
* Change settings for documentation generation.
* Generate documentation in `docs` folder instead of `gh_pages` branch.

### 0.1.1

* Upgrade dependencies (especially Glob, which now targets `netstandard2.0`).

### 0.1.0

* Initial release.
