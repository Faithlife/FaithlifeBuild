using Microsoft.Build.Framework;

namespace Faithlife.Build.Tasks;

public sealed class RewritePackageReadmeLinks : Microsoft.Build.Utilities.Task
{
	[Required]
	public ITaskItem[] SourceFile { get; set; } = [];

	[Required]
	public string OutputFile { get; set; } = "";

	[Required]
	public string RepositoryUrl { get; set; } = "";

	public override bool Execute()
	{
		if (SourceFile.Length != 1)
		{
			Log.LogError("RewritePackageReadmeLinks requires exactly one SourceFile item.");
			return false;
		}

		var sourceFile = SourceFile[0].ItemSpec;
		var outputDirectory = Path.GetDirectoryName(OutputFile);
		if (!string.IsNullOrEmpty(outputDirectory))
			Directory.CreateDirectory(outputDirectory);

		var markdown = File.ReadAllText(sourceFile);
		var rewrittenMarkdown = PackageReadmeLinkRewriter.Rewrite(markdown, new PackageReadmeLinkRewriteSettings
		{
			SourceFile = sourceFile,
			RepositoryUrl = RepositoryUrl,
		});
		File.WriteAllText(OutputFile, rewrittenMarkdown);

		return !Log.HasLoggedErrors;
	}
}
