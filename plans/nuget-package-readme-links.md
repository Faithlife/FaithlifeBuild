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

Example transform:

- `./src/Faithlife.Build/BuildApp.cs` -> `https://github.com/Faithlife/FaithlifeBuild/blob/<revision>/src/Faithlife.Build/BuildApp.cs`
- `./src/Faithlife.Build` -> `https://github.com/Faithlife/FaithlifeBuild/tree/<revision>/src/Faithlife.Build`
- `./CONTRIBUTING.md` -> `https://github.com/Faithlife/FaithlifeBuild/blob/<revision>/CONTRIBUTING.md`
- `#create-net-targets` stays unchanged.
- `https://...`, `mailto:...`, and other absolute URI links stay unchanged.

Recommended implementation details:

- Use a Markdown parser rather than broad string replacement so only actual link and image destinations are rewritten.
- Treat same-document anchors as local and leave them untouched.
- Resolve relative paths from the README file's directory, normalize `.` and `..`, and preserve any fragment or query string.
- Prefer immutable commit URLs using the source revision when available; fall back to the repository's default branch when building without source revision metadata.
- Use `blob` URLs for files and `tree` URLs for directories when the target exists in the working tree.

Extensibility path:

- Put the transform in shared build infrastructure rather than in this one project file.
- Expose a small configuration surface, for example:
  - `PackageReadmeSourceFile`, defaulting to `README.md` when `PackageReadmeFile` points at a repo README.
  - `PackageReadmeRepositoryBaseUrl`, inferred from `RepositoryUrl` when possible.
  - `PackageReadmeGitRef`, inferred from Source Link/source revision metadata or an explicit property.
  - `PackageReadmeRewriteLinks`, defaulting to `true` only when opted in initially.
- Once proven, consider making the behavior part of Faithlife.Build's standard package target or an imported MSBuild target used by Faithlife repositories.

Pros:

- Keeps one authored README.
- Gives nuget.org stable, valid links.
- Scales cleanly to other repositories.
- Can be validated automatically in CI.

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

The shared implementation should have two pieces: a link rewriter and an MSBuild integration point.

The link rewriter should accept these inputs:

- `SourceFile`: the authored README, usually the repository root `README.md`.
- `OutputFile`: the generated README that will be packed into the NuGet package.
- `RepositoryUrl`: the canonical GitHub repository URL, preferably inferred from `RepositoryUrl` or `PackageProjectUrl` metadata.
- `GitRef`: the commit SHA, tag, or branch used in generated GitHub URLs.
- `RepositoryRoot`: the repository root used to calculate the path portion of generated GitHub URLs. Relative Markdown links are resolved from the source README directory first, then made relative to this root.

The link rewriter should produce a byte-for-byte normal Markdown file except for rewritten link destinations. It should rewrite Markdown inline links, image links, and reference-style link definitions. It should not rewrite links inside code spans or fenced code blocks.

Recommended rewrite rules:

- Leave absolute URIs unchanged, including `http`, `https`, `mailto`, and protocol-relative URLs.
- Leave same-document anchors unchanged, such as `#create-net-targets`.
- Leave NuGet-supported package-relative image assets unchanged only if they are intentionally packed with the README; otherwise rewrite them like other repository-relative paths.
- Rewrite root-relative and dot-relative repository paths to GitHub URLs.
- Preserve fragments and query strings after rewriting the path.
- Use a `blob` URL when the resolved target is a file.
- Use a `tree` URL when the resolved target is a directory.
- Use a `blob` URL when the target does not exist locally but has a file-like extension; otherwise use `tree` as the conservative fallback.

The MSBuild integration should run before NuGet generates the package manifest, not after `dotnet pack` has already assembled package contents. A target such as `GenerateNuGetPackageReadme` can create the generated file, add it to the package root, and keep `PackageReadmeFile` set to `README.md` because NuGet expects that property to be the path inside the package.

Sketch:

```xml
<Target Name="GenerateNuGetPackageReadme" BeforeTargets="GenerateNuspec" Condition="'$(GenerateNuGetPackageReadme)' == 'true'">
  <PropertyGroup>
    <PackageReadmeSourceFile Condition="'$(PackageReadmeSourceFile)' == ''">$(MSBuildProjectDirectory)\$(PackageReadmeFile)</PackageReadmeSourceFile>
    <PackageReadmeRepositoryRoot Condition="'$(PackageReadmeRepositoryRoot)' == ''">$([System.IO.Path]::GetDirectoryName('$(PackageReadmeSourceFile)'))</PackageReadmeRepositoryRoot>
    <GeneratedNuGetPackageReadmeFile>$(IntermediateOutputPath)PackageReadme\README.md</GeneratedNuGetPackageReadmeFile>
  </PropertyGroup>

  <MakeDir Directories="$([System.IO.Path]::GetDirectoryName('$(GeneratedNuGetPackageReadmeFile)'))" />

  <RewritePackageReadmeLinks
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

## Proposed Phases

### Phase 1: Prototype in this repository

Implement the feature locally in `Faithlife.Build` before generalizing it.

- Add a small README rewrite utility in the build code path. For the prototype, this can be a private helper or MSBuild task invoked only by this package.
- Use a Markdown parser so the implementation walks links and images in the parsed document instead of searching the whole README text.
- Generate `artifacts/PackageReadme/Faithlife.Build/README.md` or an equivalent intermediate output from the root `README.md`.
- Derive the GitHub base URL from the existing `GitHubOrganization`, `RepositoryName`, and `RepositoryUrl` properties.
- Derive the Git ref from Source Link/source revision metadata when available; use `master` as the local fallback for this repository.
- Update `src/Faithlife.Build/Faithlife.Build.csproj` so the generated README is the file packed at the package root as `README.md`.
- Build a local package and inspect the `.nupkg` contents to confirm `README.md` is generated and its source-code links point at GitHub.

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
[BuildApp](https://github.com/Faithlife/FaithlifeBuild/blob/<revision>/src/Faithlife.Build/BuildApp.cs)
```

### Phase 2: Harden link handling

Turn the prototype into production-quality behavior.

- Support inline links, image links, shortcut references, collapsed references, full reference definitions, and autolinks when the parser exposes them as link nodes.
- Preserve anchors, query strings, URL encoding, and original link text.
- Normalize Windows and POSIX path separators before generating GitHub URLs.
- Avoid rewriting links inside code spans, fenced code blocks, HTML blocks, and raw HTML attributes unless a later requirement explicitly needs HTML handling.
- Distinguish existing files from directories for `blob` versus `tree` URLs.
- Add unit tests for common README link forms, false positives, missing targets, fragments, spaces in paths, and source READMEs that live below the repository root.
- Add an integration test that packs a sample project and opens the `.nupkg` to verify the generated README is the packaged README.

Useful test examples:

```md
[source](./src/Project/Class.cs)
[folder](./src/Project)
[with anchor](./README.md#usage)
![diagram](./docs/diagram.png)
[ref]: ./CONTRIBUTING.md
```

Expected output should preserve Markdown shape while changing only the relative destinations.

### Phase 3: Make it reusable

Move the hardened implementation into shared Faithlife.Build packaging infrastructure.

- Ship the MSBuild target in the Faithlife.Build package alongside existing build assets.
- Ship the rewrite task in an assembly that the target can load without each repository adding a new package reference.
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

Example for a repository that wants branch-based links instead of commit-based links in local packages:

```xml
<PropertyGroup>
  <PackageReadmeFile>README.md</PackageReadmeFile>
  <GenerateNuGetPackageReadme>true</GenerateNuGetPackageReadme>
  <PackageReadmeGitRef>master</PackageReadmeGitRef>
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
