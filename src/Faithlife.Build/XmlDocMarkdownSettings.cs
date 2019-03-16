using System.Collections.Generic;

namespace Faithlife.Build
{
	public sealed class XmlDocMarkdownSettings
	{
		public IReadOnlyList<string> Projects { get; set; }

		public string RepoUrl { get; set; }

		public string SourceUrl { get; set; }
	}
}
