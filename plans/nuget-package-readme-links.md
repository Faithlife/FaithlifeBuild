# NuGet Package README Relative Links

## Context

`src/Faithlife.Build/Faithlife.Build.csproj` currently reuses the repository `README.md` as the NuGet package README by setting `PackageReadmeFile` to `README.md` and packing `..\..\README.md` at the package root. This keeps package documentation from drifting, but repo-relative links such as `./src/Faithlife.Build/BuildApp.cs` are not meaningful when nuget.org renders the README.

The goal is to preserve the single-source README workflow while making the packaged README render correctly on nuget.org. The remedy should also be easy to adopt in other repositories that pack their repository README.

## Goals

- Keep the repository README pleasant to read and edit in GitHub, locally, and in IDEs.
- Make every link in the packaged README valid from nuget.org.
- Avoid hand-maintaining a second near-duplicate README unless there is a strong reason.
- Centralize the solution so other repositories can opt in with a small, obvious configuration change.
- Add validation so future relative links do not silently regress package rendering.

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
- Fail the package target if the generated README still contains repo-relative links, except for explicitly allowed same-document anchors.

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

## Option 5: Add validation only

Keep the existing package README setup but add a package validation step that fails when `PackageReadmeFile` content contains relative links that nuget.org cannot resolve.

Pros:

- Cheap guardrail.
- Useful with any other option.
- Makes the problem visible before publishing.

Cons:

- Does not fix links by itself.
- Can push developers toward manual absolute-link edits unless paired with generation.

This should be part of the final solution, but not the whole solution.

## Recommendation

Implement Option 1, with Option 5 as a required validation layer.

Start with an opt-in generated package README for this repository. Once the behavior is proven, move the transform and validation into shared Faithlife.Build packaging infrastructure so other repositories can adopt it with minimal project-file changes.

## Proposed Phases

### Phase 1: Prototype in this repository

- Add a package README generation step before `dotnet pack`.
- Generate `artifacts/PackageReadme/Faithlife.Build/README.md` from the root `README.md`.
- Rewrite only Markdown link/image destinations that are repository-relative paths.
- Point `PackageReadmeFile` at the generated README for the package.
- Add validation that fails if unsupported relative links remain.
- Inspect the generated `.nupkg` to confirm the packaged README contains absolute links.

### Phase 2: Harden link handling

- Support inline links, images, reference links, and collapsed/full reference definitions.
- Preserve anchors, query strings, and URL encoding.
- Distinguish existing files from directories for `blob` versus `tree` URLs.
- Add tests covering common README link forms and false positives such as code blocks.

### Phase 3: Make it reusable

- Move the generator into Faithlife.Build or a small shared build tool.
- Expose MSBuild/build settings for source README, output path, repository URL, git ref, and validation behavior.
- Document the opt-in configuration for repositories that reuse their root README as `PackageReadmeFile`.
- Add a convention-based default for repositories with `GitHubOrganization`, `RepositoryName`, `RepositoryUrl`, and Source Link metadata.

### Phase 4: Adopt broadly

- Find repositories that pack `README.md` as `PackageReadmeFile`.
- Enable the shared setting in each repository.
- Publish prerelease packages or inspect generated packages to verify nuget.org-ready README output.
- Consider making the option default for Faithlife.Build-based repositories after adoption is smooth.

## Acceptance Criteria

- The repository README remains the authored source of truth.
- The `.nupkg` contains a README whose repository-relative links are absolute GitHub URLs.
- Same-document anchors still work in both GitHub and NuGet rendering.
- External links are unchanged.
- The package build fails when unsupported relative links remain in the generated package README.
- Another repository can adopt the behavior through documented build/MSBuild settings without copying custom code.
