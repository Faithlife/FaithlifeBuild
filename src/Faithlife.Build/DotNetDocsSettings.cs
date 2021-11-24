namespace Faithlife.Build
{
	/// <summary>
	/// Used to generate Markdown documentation from XML comments.
	/// </summary>
	public sealed class DotNetDocsSettings
	{
		/// <summary>
		/// The target directory for generated documentation, relative to the root of the repository; defaults to <c>"docs"</c>.
		/// </summary>
		public string? TargetDirectory { get; set; }

		/// <summary>
		/// The GitHub URL of the directory containing the source code.
		/// </summary>
		public string? SourceCodeUrl { get; set; }

		/// <summary>
		/// Credentials used to push to git.
		/// </summary>
		public GitLoginInfo? GitLogin { get; set; }

		/// <summary>
		/// Commit author used to push to git.
		/// </summary>
		public GitAuthorInfo? GitAuthor { get; set; }

		/// <summary>
		/// The URL of the git repository where documentation is generated.
		/// </summary>
		public string? GitRepositoryUrl { get; set; }

		/// <summary>
		/// The name of the git branch where documentation is generated.
		/// </summary>
		public string? GitBranchName { get; set; }

		/// <summary>
		/// A function that returns true if documentation should be generated for the project.
		/// </summary>
		public Func<string, bool>? ProjectHasDocs { get; set; }

		/// <summary>
		/// Called to find the assembly for the specified project.
		/// </summary>
		public Func<string, string?>? FindAssembly { get; set; }

		/// <summary>
		/// Called to find multiple assemblies for the specified project.
		/// </summary>
		public Func<string, IReadOnlyList<string>>? FindAssemblies { get; set; }

		/// <summary>
		/// The version of the <c>xmldocmd</c> tool to use when generating documentation.
		/// </summary>
		/// <remarks>Defaults to a stable version, which may change with new versions of <b>Faithlife.Build</b>,
		/// but will not change unless <b>Faithlife.Build</b> is updated. Ignored if <c>xmldocmd</c> is installed
		/// as a .NET local tool.</remarks>
		[Obsolete("Use XmlDocGen project.")]
		public string? ToolVersion { get; set; }

		/// <summary>
		/// Clones the settings.
		/// </summary>
		public DotNetDocsSettings Clone() => (DotNetDocsSettings) MemberwiseClone();
	}
}
