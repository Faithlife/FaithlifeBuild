namespace Faithlife.Build
{
	/// <summary>
	/// Used to generate Markdown documentation from XML comments.
	/// </summary>
	public sealed class DocumentationSettings
	{
		/// <summary>
		/// The target directory for generated documentation; defaults to <c>"docs"</c>.
		/// </summary>
		public string TargetDirectory { get; set; }

		/// <summary>
		/// The GitHub URL of the directory containing the source code.
		/// </summary>
		public string SourceCodeUrl { get; set; }

		/// <summary>
		/// Credentials used to push to git.
		/// </summary>
		public GitLoginInfo GitLogin { get; set; }

		/// <summary>
		/// Commit author used to push to git.
		/// </summary>
		public GitAuthorInfo GitAuthor { get; set; }

		/// <summary>
		/// The target framework from which to generate documentation. (Optional.)
		/// </summary>
		/// <remarks>Defaults to any target framework. Supports wildcards, e.g. <c>"netstandard*"</c>.
		/// Uses the last matching DLL when sorted by path.</remarks>
		public string TargetFramework { get; set; }
	}
}
