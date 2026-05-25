using Faithlife.Build.Tasks;
using NUnit.Framework;

namespace Faithlife.Build.Tests;

internal sealed class PackageReadmeLinkRewriterTests
{
	[Test]
	public void RewritesRelativeLinks()
	{
		using var tempDirectory = new TempDirectory();
		var readmePath = tempDirectory.WriteFile("README.md", "");

		var markdown = string.Join("\n", new[]
		{
			"[source](./src/Project/Class.cs)",
			"[folder](./src/Project)",
			"[folder slash](./src/Project/)",
			"[with anchor](./README.md#usage)",
			"[root relative](/src/Project/Class.cs)",
			"![diagram](./docs/diagram.png)",
			"[ref]: ./CONTRIBUTING.md",
		});

		var rewrittenMarkdown = Rewrite(readmePath, markdown);

		Assert.That(rewrittenMarkdown, Does.Contain("[source](https://github.com/Faithlife/FaithlifeBuild/blob/-/src/Project/Class.cs)"));
		Assert.That(rewrittenMarkdown, Does.Contain("[folder](https://github.com/Faithlife/FaithlifeBuild/blob/-/src/Project)"));
		Assert.That(rewrittenMarkdown, Does.Contain("[folder slash](https://github.com/Faithlife/FaithlifeBuild/tree/-/src/Project/)"));
		Assert.That(rewrittenMarkdown, Does.Contain("[with anchor](https://github.com/Faithlife/FaithlifeBuild/blob/-/README.md#usage)"));
		Assert.That(rewrittenMarkdown, Does.Contain("[root relative](https://github.com/Faithlife/FaithlifeBuild/blob/-/src/Project/Class.cs)"));
		Assert.That(rewrittenMarkdown, Does.Contain("![diagram](https://github.com/Faithlife/FaithlifeBuild/blob/-/docs/diagram.png)"));
		Assert.That(rewrittenMarkdown, Does.Contain("[ref]: https://github.com/Faithlife/FaithlifeBuild/blob/-/CONTRIBUTING.md"));
	}

	[Test]
	public void LeavesAbsoluteAndAnchorLinksUnchanged()
	{
		using var tempDirectory = new TempDirectory();
		var readmePath = tempDirectory.WriteFile("README.md", "");

		var markdown = string.Join("\n", new[]
		{
			"[anchor](#usage)",
			"[https](https://github.com/Faithlife/FaithlifeBuild)",
			"[mailto](mailto:support@example.com)",
			"[data](data:image/png;base64,abc)",
			"[protocol relative](//cdn.example.com/image.png)",
		});

		Assert.That(Rewrite(readmePath, markdown), Is.EqualTo(markdown));
	}

	[Test]
	public void ResolvesLinksFromSourceReadmeDirectory()
	{
		using var tempDirectory = new TempDirectory();
		Directory.CreateDirectory(Path.Combine(tempDirectory.Path, "docs"));
		var readmePath = tempDirectory.WriteFile(Path.Combine("docs", "README.md"), "");

		var rewrittenMarkdown = Rewrite(readmePath, "[source](../src/Project/Class.cs)");

		Assert.That(rewrittenMarkdown, Is.EqualTo("[source](https://github.com/Faithlife/FaithlifeBuild/blob/-/src/Project/Class.cs)"));
	}

	[Test]
	public void PreservesQueryAndFragment()
	{
		using var tempDirectory = new TempDirectory();
		var readmePath = tempDirectory.WriteFile("README.md", "");

		var rewrittenMarkdown = Rewrite(readmePath, "[source](./README.md?plain=1#usage)");

		Assert.That(rewrittenMarkdown, Is.EqualTo("[source](https://github.com/Faithlife/FaithlifeBuild/blob/-/README.md?plain=1#usage)"));
	}

	private static string Rewrite(string sourceFile, string markdown) =>
		PackageReadmeLinkRewriter.Rewrite(markdown, new PackageReadmeLinkRewriteSettings
		{
			SourceFile = sourceFile,
			RepositoryUrl = "https://github.com/Faithlife/FaithlifeBuild.git",
		});

	private sealed class TempDirectory : IDisposable
	{
		public TempDirectory()
		{
			Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
			Directory.CreateDirectory(Path);
			Directory.CreateDirectory(System.IO.Path.Combine(Path, ".git"));
		}

		public string Path { get; }

		public string WriteFile(string relativePath, string contents)
		{
			var path = System.IO.Path.Combine(Path, relativePath);
			var directory = System.IO.Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(directory))
				Directory.CreateDirectory(directory);

			File.WriteAllText(path, contents);
			return path;
		}

		public void Dispose() => Directory.Delete(Path, recursive: true);
	}
}
