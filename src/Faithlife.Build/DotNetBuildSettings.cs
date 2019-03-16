namespace Faithlife.Build
{
	public sealed class DotNetBuildSettings
	{
		public string SolutionName { get; set; }

		public string NuGetSource { get; set; }

		public XmlDocMarkdownSettings XmlDocMarkdownSettings { get; set; }

		public BuildBotSettings BuildBotSettings { get; set; }

		public string DotNetToolsDirectory { get; set; }
	}
}
