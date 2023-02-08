# Contributing

## Publishing

To publish an official release:
* Update the `<VersionPrefix>` in [`Directory.Build.props`](Directory.Build.props)
* Add a corresponding section to the top of [`ReleaseNotes.md`](ReleaseNotes.md)
* Push or create a PR for review.

### Prereleases

Certain changes can be hard to unit test, and are better tested in a real consumer project. In this case, you can publish a beta version of the library for testing.

To publish a beta:
* Create a branch on origin for the code, e.g. `5.17.0-beta`
* Update the `<VersionPrefix>` in [`Directory.Build.props`](Directory.Build.props). Do not include a version suffix. e.g., `5.17.0`.
* Add a corresponding section to the top of [`ReleaseNotes.md`](ReleaseNotes.md)
* Push or create a PR for review, targeting the new branch.
* When the branch is updated and ready to publish, run the build workflow using the branch and a build number.
  * For the build number, start at 1. Continue incrementing for each new beta release you need to publish.
* When the code is read to be be published in an official release, merge the branch into master.

## Template

* This repository uses the [`faithlife-build`](https://github.com/Faithlife/CSharpTemplate/tree/faithlife-build) template of [`Faithlife/CSharpTemplate`](https://github.com/Faithlife/CSharpTemplate).
