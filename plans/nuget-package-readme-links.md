# NuGet Package README Relative Links

## Context

`src/Faithlife.Build/Faithlife.Build.csproj` currently reuses the repository `README.md` as the NuGet package README by setting `PackageReadmeFile` to `README.md` and packing `..\..\README.md` at the package root. This keeps package documentation from drifting, but repo-relative links such as `./src/Faithlife.Build/BuildApp.cs` are not meaningful when nuget.org renders the README.

The goal is to preserve the single-source README workflow while making the packaged README render correctly on nuget.org. The remedy should also be easy to adopt in other repositories that pack their repository README.

## Goals

- Keep the repository README pleasant to read and edit in GitHub, locally, and in IDEs.
- Make every link in the packaged README valid from nuget.org.
- Avoid hand-maintaining a second near-duplicate README unless there is a strong reason.
- Centralize the solution so other repositories can opt in with a small, obvious configuration change.

## Decision

Generate the packaged README during `dotnet pack`. The repository README remains the authored source of truth. The generated README is written to an intermediate output path, has repository-relative Markdown links rewritten to absolute GitHub URLs, and is packed at the package root as `README.md`.

The feature should be shared-ready from the start. It should live in the `Faithlife.Build` package and be opt-in with one property. Additional configuration can be added later if a repository proves it needs it.

## Opt-In Property Name

Recommended name: `RewritePackageReadmeLinks`.

This name describes the behavior the consumer is opting into: keep using the package README, but rewrite its links for NuGet. It is clearer than `GenerateNuGetPackageReadme`, which can sound like it creates README content from scratch.

Other reasonable names:

- `RewriteNuGetPackageReadmeLinks`
- `RewritePackageReadmeForNuGet`
- `GenerateNuGetPackageReadmeWithAbsoluteLinks`
- `UseNuGetPackageReadmeLinkRewrite`

The examples below use `RewritePackageReadmeLinks`.

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

## Relative Link Rules

A Markdown link destination should be treated as repository-relative when all of these are true:

- The destination is not empty.
- The destination does not start with `#`.
- The destination does not start with `//`.
- The destination does not start with a URI scheme matched by `^[A-Za-z][A-Za-z0-9+.-]*:`.

This means the rewriter should rewrite these forms:

- `src/Faithlife.Build/BuildApp.cs`
- `./src/Faithlife.Build/BuildApp.cs`
- `../README.md`
- `/src/Faithlife.Build/BuildApp.cs`

And should not rewrite these forms:

- `#create-net-targets`
- `https://github.com/Faithlife/FaithlifeBuild`
- `mailto:support@example.com`
- `data:image/png;base64,...`
- `//cdn.example.com/image.png`

When rewriting a destination, split the destination into path, query, and fragment first. Use only the path to decide whether it is relative and to build the GitHub URL, then append the original query and fragment unchanged.

Path resolution rules:

- A path starting with `/` is relative to `RepositoryRoot`.
- Any other relative path is resolved from the source README directory.
- Normalize `.` and `..` segments.
- Convert path separators to `/` in generated URLs.
- Use a `blob` URL unless the original path ends in `/`.
- Use a `tree` URL when the original path ends in `/`.
- Do not check the file system to decide between `blob` and `tree`, and do not infer this from file-like extensions.

Example transform, using `-` as the GitHub ref so GitHub redirects to the repository's default branch:

- `./src/Faithlife.Build/BuildApp.cs` -> `https://github.com/Faithlife/FaithlifeBuild/blob/-/src/Faithlife.Build/BuildApp.cs`
- `./src/Faithlife.Build` -> `https://github.com/Faithlife/FaithlifeBuild/blob/-/src/Faithlife.Build`
- `./src/Faithlife.Build/` -> `https://github.com/Faithlife/FaithlifeBuild/tree/-/src/Faithlife.Build/`
- `./CONTRIBUTING.md#setup` -> `https://github.com/Faithlife/FaithlifeBuild/blob/-/CONTRIBUTING.md#setup`

## MSBuild Integration

The MSBuild integration should run before NuGet generates the package manifest, not after `dotnet pack` has already assembled package contents. A target such as `RewritePackageReadmeLinksForPack` can find the existing packed README item, generate the rewritten README, suppress packing of the source README item, add the generated README to the package root, and keep `PackageReadmeFile` set to `README.md` because NuGet expects that property to be the path inside the package.

The target should infer the source README from the existing package item instead of requiring a separate source-file property. The source README item is the `None` item whose metadata matches all of these conditions:

- `Pack` is `true`.
- `PackagePath` is empty, `\`, `/`, or `.`.
- The file name is the same as `$(PackageReadmeFile)`.

If there is not exactly one matching source README item, the target should fail with a clear message that explains which package item shape it expects.

Sketch:

```xml
<UsingTask
    TaskName="Faithlife.Build.Tasks.RewritePackageReadmeLinks"
    AssemblyFile="$(MSBuildThisFileDirectory)..\tools\net8.0\Faithlife.Build.Tasks.dll" />

<Target Name="RewritePackageReadmeLinksForPack" BeforeTargets="GenerateNuspec" Condition="'$(RewritePackageReadmeLinks)' == 'true'">
  <ItemGroup>
    <PackageReadmeSource Include="@(None)"
        Condition="'%(None.Pack)' == 'true' and ('%(None.PackagePath)' == '' or '%(None.PackagePath)' == '\' or '%(None.PackagePath)' == '/' or '%(None.PackagePath)' == '.') and '$([System.IO.Path]::GetFileName('%(None.Identity)'))' == '$(PackageReadmeFile)'" />
  </ItemGroup>

  <Error Condition="'@(PackageReadmeSource->Count())' != '1'" Text="RewritePackageReadmeLinks requires exactly one packed README item such as &lt;None Include=&quot;..\..\README.md&quot; Pack=&quot;true&quot; PackagePath=&quot;\&quot; /&gt;." />

  <PropertyGroup>
    <GeneratedNuGetPackageReadmeFile>$(IntermediateOutputPath)PackageReadme\README.md</GeneratedNuGetPackageReadmeFile>
  </PropertyGroup>

  <MakeDir Directories="$([System.IO.Path]::GetDirectoryName('$(GeneratedNuGetPackageReadmeFile)'))" />

  <Faithlife.Build.Tasks.RewritePackageReadmeLinks
      SourceFile="@(PackageReadmeSource)"
      OutputFile="$(GeneratedNuGetPackageReadmeFile)"
      RepositoryRoot="$(MSBuildProjectDirectory)\..\.."
      RepositoryUrl="$(RepositoryUrl)"
      GitRef="-" />

  <ItemGroup>
    <None Update="@(PackageReadmeSource)" Pack="false" />
    <None Include="$(GeneratedNuGetPackageReadmeFile)" Pack="true" PackagePath="\" />
  </ItemGroup>

  <PropertyGroup>
    <PackageReadmeFile>README.md</PackageReadmeFile>
  </PropertyGroup>
</Target>
```

The sketch uses `$(MSBuildProjectDirectory)\..\..` as the repository root because this repository's packable project is under `src/Faithlife.Build`. The implementation should infer the repository root in the shared build target from the nearest ancestor containing `.git` when possible. If that is not available during pack, use the directory containing the source README.

The shared target is published through the `Faithlife.Build` NuGet package. `Faithlife.Build.targets` is packed under `build/Faithlife.Build.targets` and `buildTransitive/Faithlife.Build.targets`, and `Faithlife.Build.Tasks.dll` is packed under `tools/net8.0/`. NuGet automatically imports `build/Faithlife.Build.targets` into projects that directly reference the package, and imports `buildTransitive/Faithlife.Build.targets` through project-to-project/package dependency chains. The target then loads the task assembly through the relative `UsingTask` path shown above.

For repositories that only reference `Faithlife.Build` from `tools/Build`, `DotNetBuild` can also import the same target while it runs `dotnet pack` by extending the existing runtime targets mechanism that passes `-p:DirectoryBuildTargetsPath=...`. That lets the build tool apply the shared target to packable projects without adding a direct package reference to every project being packed.

## Work Items

- Add `src/Faithlife.Build.Tasks/Faithlife.Build.Tasks.csproj`.
- Add `src/Faithlife.Build.Tasks/PackageReadmeLinkRewriter.cs`.
- Add `src/Faithlife.Build.Tasks/RewritePackageReadmeLinks.cs`.
- Add `src/Faithlife.Build/Faithlife.Build.targets` with the `UsingTask` and `RewritePackageReadmeLinksForPack` target.
- Update `src/Faithlife.Build/Faithlife.Build.csproj` to pack `Faithlife.Build.targets` under both `build/` and `buildTransitive/`.
- Update `src/Faithlife.Build/Faithlife.Build.csproj` to pack `Faithlife.Build.Tasks.dll` and any required task dependencies under `tools/net8.0/`.
- Update the existing runtime targets mechanism so Faithlife.Build-based repositories that pack via `DotNetBuild` can import the same target during `dotnet pack`.
- Add unit tests for link classification, path rewriting, fenced code blocks, inline links, image links, reference-style links, fragments, query strings, slash-terminated paths, and root-relative paths.
- Add an integration test that packs a sample project and verifies the generated README is the packaged README.

Useful test examples:

```md
[source](./src/Project/Class.cs)
[folder](./src/Project)
[folder slash](./src/Project/)
[with anchor](./README.md#usage)
[root relative](/src/Project/Class.cs)
![diagram](./docs/diagram.png)
[ref]: ./CONTRIBUTING.md
```

Expected output should preserve Markdown shape while changing only the relative destinations.

## Consuming Repository Steps

For a consuming repository that currently has only this package README item:

```xml
<None Include="..\..\README.md" Pack="true" PackagePath="\" />
```

Do this:

- Update the repository to a version of `Faithlife.Build` that contains the shared README link rewrite target.
- Keep the existing `PackageReadmeFile` property:

```xml
<PackageReadmeFile>README.md</PackageReadmeFile>
```

- Keep the existing packed README item unchanged:

```xml
<None Include="..\..\README.md" Pack="true" PackagePath="\" />
```

- Add the one opt-in property next to the existing package metadata:

```xml
<RewritePackageReadmeLinks>true</RewritePackageReadmeLinks>
```

The resulting project file should look like this in the relevant spots:

```xml
<PropertyGroup>
  <PackageReadmeFile>README.md</PackageReadmeFile>
  <RewritePackageReadmeLinks>true</RewritePackageReadmeLinks>
</PropertyGroup>

<ItemGroup>
  <None Include="..\..\README.md" Pack="true" PackagePath="\" />
</ItemGroup>
```

- Run the repository's normal package command.
- Open the generated `.nupkg` and verify that the packaged `README.md` contains absolute GitHub links for repository-relative links.
- Do not add source README path, repository URL, Git ref, or repository root properties unless a future repository demonstrates that the defaults are insufficient.

## Acceptance Criteria

- The repository README remains the authored source of truth.
- The `.nupkg` contains a README whose repository-relative links are absolute GitHub URLs.
- Same-document anchors still work in both GitHub and NuGet rendering.
- External links are unchanged.
- Another repository can adopt the behavior through documented build/MSBuild settings without copying custom code.
