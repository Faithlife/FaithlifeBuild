using System.Text.RegularExpressions;

namespace Faithlife.Build.Tasks;

public static partial class PackageReadmeLinkRewriter
{
	public static string Rewrite(string markdown, PackageReadmeLinkRewriteSettings settings)
	{
		ArgumentNullException.ThrowIfNull(markdown);
		ArgumentNullException.ThrowIfNull(settings);

		var sourceFile = Path.GetFullPath(settings.SourceFile);
		var sourceDirectory = Path.GetDirectoryName(sourceFile) ?? throw new InvalidOperationException($"Source file '{sourceFile}' does not have a directory.");
		var repositoryRoot = FindRepositoryRoot(sourceDirectory);
		var repositoryUrl = NormalizeRepositoryUrl(settings.RepositoryUrl);

		var result = InlineLinkRegex().Replace(markdown, match => RewriteMatch(match, sourceDirectory, repositoryRoot, repositoryUrl));
		return ReferenceLinkRegex().Replace(result, match => RewriteMatch(match, sourceDirectory, repositoryRoot, repositoryUrl));
	}

	private static string RewriteMatch(Match match, string sourceDirectory, string repositoryRoot, string repositoryUrl)
	{
		var destination = match.Groups["destination"].Value;
		var rewrittenDestination = RewriteDestination(destination, sourceDirectory, repositoryRoot, repositoryUrl);
		return string.Concat(match.Groups["prefix"].Value, rewrittenDestination, match.Groups["suffix"].Value);
	}

	private static string RewriteDestination(string destination, string sourceDirectory, string repositoryRoot, string repositoryUrl)
	{
		var hasAngleBrackets = destination.Length >= 2 && destination[0] == '<' && destination[^1] == '>';
		var value = hasAngleBrackets ? destination[1..^1] : destination;

		var rewrittenValue = RewriteDestinationValue(value, sourceDirectory, repositoryRoot, repositoryUrl);
		return hasAngleBrackets ? $"<{rewrittenValue}>" : rewrittenValue;
	}

	private static string RewriteDestinationValue(string destination, string sourceDirectory, string repositoryRoot, string repositoryUrl)
	{
		if (!IsRepositoryRelativeDestination(destination))
			return destination;

		var (path, suffix) = SplitPathAndSuffix(destination);
		if (path.Length == 0)
			return destination;

		var useTree = path.EndsWith('/') || path.EndsWith('\\');
		var fullPath = Path.GetFullPath(path.StartsWith('/') || path.StartsWith('\\') ?
			Path.Combine(repositoryRoot, path.TrimStart('/', '\\')) :
			Path.Combine(sourceDirectory, path));
		var relativePath = Path.GetRelativePath(repositoryRoot, fullPath).Replace('\\', '/');
		if (useTree && !relativePath.EndsWith('/'))
			relativePath += "/";

		var escapedPath = EscapePath(relativePath);
		var kind = useTree ? "tree" : "blob";
		return $"{repositoryUrl}/{kind}/-/{escapedPath}{suffix}";
	}

	private static bool IsRepositoryRelativeDestination(string destination) =>
		destination.Length != 0 &&
		!destination.StartsWith('#') &&
		!destination.StartsWith("//", StringComparison.Ordinal) &&
		!UriSchemeRegex().IsMatch(destination);

	private static (string Path, string Suffix) SplitPathAndSuffix(string destination)
	{
		var queryIndex = destination.IndexOf('?', StringComparison.Ordinal);
		var fragmentIndex = destination.IndexOf('#', StringComparison.Ordinal);
		var suffixIndex = queryIndex == -1 ? fragmentIndex : fragmentIndex == -1 ? queryIndex : Math.Min(queryIndex, fragmentIndex);
		return suffixIndex == -1 ? (destination, "") : (destination[..suffixIndex], destination[suffixIndex..]);
	}

	private static string FindRepositoryRoot(string sourceDirectory)
	{
		var directory = new DirectoryInfo(sourceDirectory);
		while (directory is not null)
		{
			if (Directory.Exists(Path.Combine(directory.FullName, ".git")) || File.Exists(Path.Combine(directory.FullName, ".git")))
				return directory.FullName;

			directory = directory.Parent;
		}

		return sourceDirectory;
	}

	private static string NormalizeRepositoryUrl(string repositoryUrl)
	{
		if (repositoryUrl.StartsWith("git@github.com:", StringComparison.Ordinal))
			repositoryUrl = "https://github.com/" + repositoryUrl["git@github.com:".Length..];

		repositoryUrl = repositoryUrl.TrimEnd('/');
		return repositoryUrl.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? repositoryUrl[..^4] : repositoryUrl;
	}

	private static string EscapePath(string relativePath) => string.Join('/', relativePath.Split('/').Select(Uri.EscapeDataString));

	[GeneratedRegex(@"(?<prefix>!?\[[^\]\r\n]*\]\()(?<destination><[^>\r\n]*>|[^\s\)\r\n]+)(?<suffix>(?:\s+[^\)\r\n]*)?\))")]
	private static partial Regex InlineLinkRegex();

	[GeneratedRegex(@"(?m)^(?<prefix>[ \t]{0,3}\[[^\]\r\n]+\]:[ \t]*)(?<destination><[^>\r\n]*>|[^\s\r\n]+)(?<suffix>.*)$")]
	private static partial Regex ReferenceLinkRegex();

	[GeneratedRegex(@"^[A-Za-z][A-Za-z0-9+.-]*:")]
	private static partial Regex UriSchemeRegex();
}
