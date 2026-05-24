# NuGet Package README Relative Links

## Context

`src/Faithlife.Build/Faithlife.Build.csproj` currently reuses the repository `README.md` as the NuGet package README by setting `PackageReadmeFile` to `README.md` and packing `..\..\README.md` at the package root. This keeps package documentation from drifting, but repo-relative links such as `./src/Faithlife.Build/BuildApp.cs` are not meaningful when nuget.org renders the README.

The goal is to preserve the single-source README workflow while making the packaged README render correctly on nuget.org. The remedy should also be easy to adopt in other repositories that pack their repository README.

## Goals

- Keep the repository README pleasant to read and edit in GitHub, locally, and in IDEs.
- Make every link in the packaged README valid from nuget.org.
- Avoid hand-maintaining a second near-duplicate README unless there is a strong reason.
- Centralize the solution so other repositories can opt in with a small, obvious configuration change.

## Option 1: Generate a NuGet-specific README during pack

Create a build step that reads the repository README, rewrites repo-relative Markdown links to absolute GitHub URLs, writes the transformed file under `artifacts`, and configures `PackageReadmeFile`/`None Pack="true"` to include that generated file instead of the source README.

Example transform, using `-` as the GitHub ref so GitHub redirects to the repository's default branch:

- `./src/Faithlife.Build/BuildApp.cs` -> `https://github.com/Faithlife/FaithlifeBuild/blob/-/src/Faithlife.Build/BuildApp.cs`
- `./src/Faithlife.Build` -> `https://github.com/Faithlife/FaithlifeBuild/blob/-/src/Faithlife.Build`
- `./src/Faithlife.Build/` -> `https://github.com/Faithlife/FaithlifeBuild/tree/-/src/Faithlife.Build/`
- `./CONTRIBUTING.md` -> `https://github.com/Faithlife/FaithlifeBuild/blob/-/CONTRIBUTING.md`
- `#create-net-targets` stays unchanged.
- `https://...`, `mailto:...`, and other absolute URI links stay unchanged.

Recommended implementation details:

- Use targeted regular expressions for Markdown link and image destinations rather than broad string replacement across the whole README.
- Treat same-document anchors as local and leave them untouched.
- Resolve relative paths from the README file's directory, normalize `.` and `..`, and preserve any fragment or query string.
- Use `-` as the default GitHub ref so published package READMEs resolve through the repository's default branch.
- Use `blob` URLs unless the original path ends in a slash; use `tree` URLs only for paths that end in a slash. GitHub will redirect `blob` to `tree` when the target is a directory.

Extensibility path:

- Put the transform in shared build infrastructure rather than in this one project file.
- Expose a small configuration surface, for example:
  - `PackageReadmeSourceFile`, defaulting to `README.md` when `PackageReadmeFile` points at a repo README.
  - `PackageReadmeRepositoryBaseUrl`, inferred from `RepositoryUrl` when possible.
  - `PackageReadmeGitRef`, defaulting to `-` and overridable when a repository wants a different ref.
  - `PackageReadmeRewriteLinks`, defaulting to `true` only when opted in initially.
- Once proven, consider making the behavior part of Faithlife.Build's standard package target or an imported MSBuild target used by Faithlife repositories.

Pros:

- Keeps one authored README.
- Gives nuget.org stable, valid links.
- Scales cleanly to other repositories.

Cons:

- Requires a small tool or MSBuild task.
- Needs careful handling of Markdown edge cases, especially images, reference links, fragments, and paths with spaces.

## Option 2: Change repository README links to absolute GitHub URLs

Replace repo-relative links in `README.md` with absolute `https://github.com/Faithlife/FaithlifeBuild/...` links.

Pros:

- Lowest implementation cost.
- No build tooling changes.
- NuGet and GitHub both render valid links immediately.

Cons:

- Less pleasant for local editing and forks.
- Links must choose a branch or commit; branch URLs can drift, while commit URLs are awkward to maintain in source.
- Every repository has to do the same manual conversion.
- No general mechanism prevents future relative links from being added.

This is a reasonable short-term workaround, but it is not the best reusable solution.

## Option 3: Maintain a package-specific README

Create a dedicated package README, such as `README.Package.md`, with absolute links and NuGet-focused wording. Keep the repository README unchanged.

Pros:

- Simple package behavior.
- Allows package-specific structure and wording.
- Avoids Markdown transformation complexity.

Cons:

- Introduces duplicate documentation that can drift.
- Requires human review to keep two README files aligned.
- Does not generalize well unless paired with shared templates or generation.

This option is most attractive when the NuGet README should intentionally differ from the repository README, not just when links need fixing.

## Option 4: Generate a package README from shared fragments

Split README content into reusable fragments, then generate both the repository README and NuGet README from the same source content with different link bases.

Pros:

- Avoids duplication while allowing channel-specific output.
- Can grow into a broader docs generation pipeline.

Cons:

- More process and tooling than the current problem requires.
- Makes simple README edits less direct.
- Existing repositories would need to restructure documentation to adopt it.

This is only worth considering if multiple repositories already need more substantial README generation.

## Recommendation

Implement Option 1.

Start with an opt-in generated package README for this repository. Once the behavior is proven, move the transform into shared Faithlife.Build packaging infrastructure so other repositories can adopt it with minimal project-file changes.

## Implementation Spec

The shared implementation should have two pieces: a C# link rewriting helper and an MSBuild integration point.

The link rewriter is a small C# component that transforms README Markdown text. It is not a separate command-line tool and it is not a second README. The concrete reusable shape should be:

- `src/Faithlife.Build.Tasks/Faithlife.Build.Tasks.csproj`: a task assembly that can be loaded by MSBuild.
- `src/Faithlife.Build.Tasks/PackageReadmeLinkRewriter.cs`: a pure C# helper that reads Markdown text, rewrites link destinations, and returns the transformed Markdown.
- `src/Faithlife.Build.Tasks/RewritePackageReadmeLinks.cs`: an MSBuild task wrapper that accepts MSBuild properties, calls `PackageReadmeLinkRewriter`, and writes the generated README file.
- `src/Faithlife.Build/Faithlife.Build.targets`: the MSBuild target file shipped in the NuGet package to call `RewritePackageReadmeLinks` before packing.

The link rewriter should accept these inputs:

- `SourceFile`: the authored README, usually the repository root `README.md`.
- `OutputFile`: the generated README that will be packed into the NuGet package.
- `RepositoryUrl`: the canonical GitHub repository URL, preferably inferred from `RepositoryUrl` or `PackageProjectUrl` metadata.
- `GitRef`: the GitHub ref used in generated GitHub URLs. Default this to `-` so links redirect to the default branch.
- `RepositoryRoot`: the repository root used to calculate the path portion of generated GitHub URLs. Relative Markdown links are resolved from the source README directory first, then made relative to this root.

The link rewriter should produce normal Markdown with only link destinations changed. Use regular expressions for the Markdown link forms this plan supports: inline links, image links, and reference-style link definitions. Before applying those expressions, split or scan the document so fenced code blocks are copied through unchanged.

Recommended rewrite rules:

- Leave absolute URIs unchanged, including `http`, `https`, `mailto`, and protocol-relative URLs.
- Leave same-document anchors unchanged, such as `#create-net-targets`.
- Leave NuGet-supported package-relative image assets unchanged only if they are intentionally packed with the README; otherwise rewrite them like other repository-relative paths.
- Rewrite root-relative and dot-relative repository paths to GitHub URLs.
- Preserve fragments and query strings after rewriting the path.
- Use a `blob` URL unless the original path ends in a slash.
- Use a `tree` URL when the original path ends in a slash.
- Do not check the file system to decide between `blob` and `tree`, and do not infer this from file-like extensions.

The MSBuild integration should run before NuGet generates the package manifest, not after `dotnet pack` has already assembled package contents. A target such as `GenerateNuGetPackageReadme` can create the generated file, add it to the package root, and keep `PackageReadmeFile` set to `README.md` because NuGet expects that property to be the path inside the package.

Sketch:

```xml
<UsingTask
    TaskName="Faithlife.Build.Tasks.RewritePackageReadmeLinks"
    AssemblyFile="$(MSBuildThisFileDirectory)..\tools\net8.0\Faithlife.Build.Tasks.dll" />

<Target Name="GenerateNuGetPackageReadme" BeforeTargets="GenerateNuspec" Condition="'$(GenerateNuGetPackageReadme)' == 'true'">
  <PropertyGroup>
    <PackageReadmeSourceFile Condition="'$(PackageReadmeSourceFile)' == ''">$(MSBuildProjectDirectory)\$(PackageReadmeFile)</PackageReadmeSourceFile>
    <PackageReadmeRepositoryRoot Condition="'$(PackageReadmeRepositoryRoot)' == ''">$([System.IO.Path]::GetDirectoryName('$(PackageReadmeSourceFile)'))</PackageReadmeRepositoryRoot>
    <PackageReadmeGitRef Condition="'$(PackageReadmeGitRef)' == ''">-</PackageReadmeGitRef>
    <GeneratedNuGetPackageReadmeFile>$(IntermediateOutputPath)PackageReadme\README.md</GeneratedNuGetPackageReadmeFile>
  </PropertyGroup>

  <MakeDir Directories="$([System.IO.Path]::GetDirectoryName('$(GeneratedNuGetPackageReadmeFile)'))" />

  <Faithlife.Build.Tasks.RewritePackageReadmeLinks
      SourceFile="$(PackageReadmeSourceFile)"
      OutputFile="$(GeneratedNuGetPackageReadmeFile)"
      RepositoryRoot="$(PackageReadmeRepositoryRoot)"
      RepositoryUrl="$(RepositoryUrl)"
      GitRef="$(PackageReadmeGitRef)" />

  <ItemGroup>
    <None Include="$(GeneratedNuGetPackageReadmeFile)" Pack="true" PackagePath="\" />
  </ItemGroup>

  <PropertyGroup>
    <PackageReadmeFile>README.md</PackageReadmeFile>
  </PropertyGroup>
</Target>
```

The exact target and property names can change during implementation, but the important shape is that projects opt in with a property, the generated file is the file packed at `README.md`, and the authored repository README remains the source file.

The shared target is published through the `Faithlife.Build` NuGet package. `Faithlife.Build.targets` is packed under `build/Faithlife.Build.targets` and `buildTransitive/Faithlife.Build.targets`, and `Faithlife.Build.Tasks.dll` is packed under `tools/net8.0/`. NuGet automatically imports `build/Faithlife.Build.targets` into projects that directly reference the package, and imports `buildTransitive/Faithlife.Build.targets` through project-to-project/package dependency chains. The target then loads the task assembly through the relative `UsingTask` path shown above.

For repositories that only reference `Faithlife.Build` from `tools/Build`, `DotNetBuild` can also import the same target while it runs `dotnet pack` by extending the existing runtime targets mechanism that passes `-p:DirectoryBuildTargetsPath=...`. That lets the build tool apply the shared target to packable projects without adding a direct package reference to every project being packed.

## Proposed Phases

### Phase 1: Prototype in this repository

Implement the feature locally in `Faithlife.Build` before generalizing it.

- Add a small C# README rewrite utility in the build code path.
- Use targeted regular expressions for Markdown links and images, with fenced code blocks excluded before link rewriting.
- Generate `artifacts/PackageReadme/Faithlife.Build/README.md` or an equivalent intermediate output from the root `README.md`.
- Derive the GitHub base URL from the existing `GitHubOrganization`, `RepositoryName`, and `RepositoryUrl` properties.
- Use `-` as the Git ref so package links redirect to the default branch where published package contents came from.
- Update `src/Faithlife.Build/Faithlife.Build.csproj` so the generated README is the file packed at the package root as `README.md`.
- Build a local package and inspect the `.nupkg` contents to confirm `README.md` is generated and its source-code links point at GitHub.

Private helper prototype:

- Add `src/Faithlife.Build/PackageReadmeLinkRewriter.cs` with a static C# method such as `RewriteFile(PackageReadmeRewriteSettings settings)`.
- Add `src/Faithlife.Build/PackageReadmeRewriteSettings.cs` if the argument list becomes awkward.
- Call the helper from `DotNetBuild.BuildNuGetPackages` before invoking `dotnet pack`.
- Generate a temporary targets file, similar to the existing runtime targets file, that adds the generated README to the package root with `PackagePath="\"`.
- Pass that temporary targets file to `dotnet pack` with the existing `DirectoryBuildTargetsPath` mechanism.

Sketch:

```csharp
var generatedReadmePath = PackageReadmeLinkRewriter.RewriteFile(new PackageReadmeRewriteSettings
{
  SourceFile = Path.GetFullPath("README.md"),
  OutputFile = Path.Combine("artifacts", "PackageReadme", packageId, "README.md"),
  RepositoryRoot = Directory.GetCurrentDirectory(),
  RepositoryUrl = "https://github.com/Faithlife/FaithlifeBuild",
  GitRef = "-",
});
```

MSBuild task prototype:

- Add `src/Faithlife.Build.Tasks/Faithlife.Build.Tasks.csproj`.
- Add `src/Faithlife.Build.Tasks/PackageReadmeLinkRewriter.cs` for the pure rewrite logic.
- Add `src/Faithlife.Build.Tasks/RewritePackageReadmeLinks.cs` as the MSBuild task wrapper.
- Add `src/Faithlife.Build/Faithlife.Build.targets` with the `UsingTask` and `GenerateNuGetPackageReadme` target shown above.
- Pack the targets file and task assembly only for this repository at first, then generalize the package layout in Phase 3.

The private helper is fastest to prove inside the current build flow. The MSBuild task is closer to the final reusable design because the pack project itself owns the generated item that NuGet includes in the package.

Example prototype usage in this repository:

```xml
<PropertyGroup>
  <PackageReadmeFile>README.md</PackageReadmeFile>
  <GenerateNuGetPackageReadme>true</GenerateNuGetPackageReadme>
  <PackageReadmeSourceFile>$(MSBuildProjectDirectory)\..\..\README.md</PackageReadmeSourceFile>
</PropertyGroup>
```

After `dotnet pack`, the package should contain `README.md`, but that file should be generated from the repository README and contain links like:

```md
[BuildApp](https://github.com/Faithlife/FaithlifeBuild/blob/-/src/Faithlife.Build/BuildApp.cs)
```

### Phase 2: Harden link handling

Turn the prototype into production-quality behavior.

- Support inline links, image links, shortcut references, collapsed references, and full reference definitions with regular expressions that target Markdown link destinations.
- Preserve anchors, query strings, URL encoding, and original link text.
- Normalize Windows and POSIX path separators before generating GitHub URLs.
- Avoid rewriting links inside fenced code blocks. HTML blocks and raw HTML attributes can remain out of scope unless a later repository needs them.
- Use `blob` URLs unless the original path ends in a slash, and use `tree` URLs only when the original path ends in a slash.
- Add unit tests for common README link forms, false positives, missing targets, fragments, spaces in paths, slash-terminated directory links, and source READMEs that live below the repository root.
- Add an integration test that packs a sample project and opens the `.nupkg` to verify the generated README is the packaged README.

Useful test examples:

```md
[source](./src/Project/Class.cs)
[folder](./src/Project)
[folder slash](./src/Project/)
[with anchor](./README.md#usage)
![diagram](./docs/diagram.png)
[ref]: ./CONTRIBUTING.md
```

Expected output should preserve Markdown shape while changing only the relative destinations.

### Phase 3: Make it reusable

Move the hardened implementation into shared Faithlife.Build packaging infrastructure.

- Ship `src/Faithlife.Build/Faithlife.Build.targets` in the Faithlife.Build package alongside existing build assets.
- Ship `Faithlife.Build.Tasks.dll` in the Faithlife.Build package under `tools/net8.0/` so the target can load it with `UsingTask`.
- Update `src/Faithlife.Build/Faithlife.Build.csproj` to pack `Faithlife.Build.targets` under both `build/` and `buildTransitive/`, matching the existing `Faithlife.Build.props` pattern.
- Update the package project to pack `Faithlife.Build.Tasks.dll` and any required task dependencies under `tools/net8.0/`.
- For standard Faithlife.Build-based repositories, update the runtime targets mechanism so `DotNetBuild` imports the shared target during `dotnet pack`; this avoids a direct `PackageReference` in every packable project.
- Expose MSBuild/build settings for source README, generated output path, repository URL, git ref, and repository root.
- Document the opt-in configuration for repositories that reuse their root README as `PackageReadmeFile`.
- Add convention-based defaults for repositories with `GitHubOrganization`, `RepositoryName`, `RepositoryUrl`, and Source Link metadata.
- Keep project-level customization small; most repositories should only set `GenerateNuGetPackageReadme` and, when needed, `PackageReadmeSourceFile`.

Example consumer project after the shared target exists:

```xml
<PropertyGroup>
  <PackageReadmeFile>README.md</PackageReadmeFile>
  <GenerateNuGetPackageReadme>true</GenerateNuGetPackageReadme>
</PropertyGroup>
```

Example for a package project below `src/` that uses the root README:

```xml
<PropertyGroup>
  <PackageReadmeFile>README.md</PackageReadmeFile>
  <GenerateNuGetPackageReadme>true</GenerateNuGetPackageReadme>
  <PackageReadmeSourceFile>$(MSBuildProjectDirectory)\..\..\README.md</PackageReadmeSourceFile>
</PropertyGroup>
```

Example for a repository that wants to be explicit about default-branch links:

```xml
<PropertyGroup>
  <PackageReadmeFile>README.md</PackageReadmeFile>
  <GenerateNuGetPackageReadme>true</GenerateNuGetPackageReadme>
  <PackageReadmeGitRef>-</PackageReadmeGitRef>
</PropertyGroup>
```

### Phase 4: Adopt broadly

Roll the shared behavior out to other repositories that have the same README reuse pattern.

- Find repositories that pack `README.md` as `PackageReadmeFile`, especially those using a root README from a package project under `src/`.
- Enable the shared setting in each repository with the smallest possible project-file change.
- Build packages locally and inspect generated package READMEs before publishing.
- Update repository templates or coding guidelines so new packages can opt in from the start.
- Consider making the option default for Faithlife.Build-based repositories after enough packages have adopted it without surprises.

## Acceptance Criteria

- The repository README remains the authored source of truth.
- The `.nupkg` contains a README whose repository-relative links are absolute GitHub URLs.
- Same-document anchors still work in both GitHub and NuGet rendering.
- External links are unchanged.
- Another repository can adopt the behavior through documented build/MSBuild settings without copying custom code.
