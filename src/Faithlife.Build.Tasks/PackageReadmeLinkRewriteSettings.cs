namespace Faithlife.Build.Tasks;

public sealed class PackageReadmeLinkRewriteSettings
{
	public required string SourceFile { get; init; }

	public required string RepositoryUrl { get; init; }
}
