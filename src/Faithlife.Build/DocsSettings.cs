using System.Collections.Generic;

namespace Faithlife.Build
{
	/// <summary>
	/// Settings used when generating markdown from XML documentation comments; see <see cref="DotNetBuildSettings"/>.
	/// </summary>
	public sealed class DocsSettings
	{
		/// <summary>
		/// The projects for which to generate markdown documentation.
		/// </summary>
		public IReadOnlyList<string> Projects { get; set; }

		/// <summary>
		/// The GitHub URL of the parent directory of the source code of the projects.
		/// </summary>
		public string SourceUrl { get; set; }
	}
}
