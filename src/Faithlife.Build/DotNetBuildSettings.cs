namespace Faithlife.Build
{
	/// <summary>
	/// Settings for a .NET build; see <see cref="DotNetBuild"/>.
	/// </summary>
	public sealed class DotNetBuildSettings
	{
		/// <summary>
		/// The name of the solution file. Optional if there is only one solution in the working directory.
		/// </summary>
		public string SolutionName { get; set; }

		/// <summary>
		/// The NuGet source to which to push packages. The standard public NuGet source if omitted.
		/// </summary>
		public string NuGetSource { get; set; }

		/// <summary>
		/// Settings used to generate markdown from XML documentation comments.
		/// </summary>
		public XmlDocMarkdownSettings XmlDocMarkdownSettings { get; set; }

		/// <summary>
		/// Credentials used to push to git as needed.
		/// </summary>
		public GitLoginInfo GitLogin { get; set; }

		/// <summary>
		/// Commit author used to push to git as needed.
		/// </summary>
		public GitAuthorInfo GitAuthor { get; set; }

		/// <summary>
		/// The directory where local .NET Core tools can be installed.
		/// </summary>
		public string DotNetToolsDirectory { get; set; }
	}
}
