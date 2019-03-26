# Version History

## Pending

Describe changes here when they're committed to the `master` branch. Move them to **Released** when the project version number is updated in preparation for publishing an updated NuGet package.

Prefix the description of the change with `[major]`, `[minor]` or `[patch]` in accordance with [Semantic Versioning](https://semver.org/).

## Released

### 0.4.0

* [minor] Use `DotNetBuildOptions` to access and/or override build options.
* [minor] Automatically run any target named `default`.
* [minor] Return existing target; allow multiple actions per target.
* [minor] Don't crash if a packaged project doesn't output a DLL.
* [minor] Support `update-docs` publish trigger for .NET builds.

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
