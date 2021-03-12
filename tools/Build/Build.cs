using System;
using Faithlife.Build;

return BuildRunner.Execute(args, build => build.AddDotNetTargets(
	new DotNetBuildSettings
	{
		NuGetApiKey = Environment.GetEnvironmentVariable("NUGET_API_KEY"),
		GitLogin = new GitLoginInfo("faithlifebuildbot", Environment.GetEnvironmentVariable("BUILD_BOT_PASSWORD") ?? ""),
		PackageSettings = new DotNetPackageSettings { PushTagOnPublish = x => $"v{x.Version}" },
		DocsSettings = new DotNetDocsSettings
		{
			GitAuthor = new GitAuthorInfo("Faithlife Build Bot", "faithlifebuildbot@users.noreply.github.com"),
			SourceCodeUrl = "https://github.com/Faithlife/FaithlifeBuild/tree/master/src",
		},
	}));
