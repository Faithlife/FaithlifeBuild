using System;
using Faithlife.Build;

internal static class Build
{
	public static int Main(string[] args) => BuildRunner.Execute(args, build =>
	{
		build.AddDotNetTargets(
			new DotNetBuildSettings
			{
				Verbosity = DotNetBuildVerbosity.Minimal,
				NuGetApiKey = Environment.GetEnvironmentVariable("NUGET_API_KEY"),
				DocsSettings = new DotNetDocsSettings
				{
					GitLogin = new GitLoginInfo("faithlifebuildbot", Environment.GetEnvironmentVariable("BUILD_BOT_PASSWORD") ?? ""),
					GitAuthor = new GitAuthorInfo("Faithlife Build Bot", "faithlifebuildbot@users.noreply.github.com"),
					SourceCodeUrl = "https://github.com/Faithlife/FaithlifeBuild/tree/master/src",
					GitBranchName = GetGitBranchName(),
				},
			});
	});

	private static string? GetGitBranchName()
	{
		const string prefix = "refs/heads/";
		return Environment.GetEnvironmentVariable("GITHUB_REF") is string githubRef && githubRef.StartsWith("refs/heads/", StringComparison.Ordinal) ? githubRef.Substring(prefix.Length) : null;
	}
}
