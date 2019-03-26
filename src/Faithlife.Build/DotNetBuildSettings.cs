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
		/// Used to install .NET Core Global tools.
		/// </summary>
		/// <remarks>Optional. If not specified, installs tools under <c>tools/bin</c></remarks>
		public DotNetTools DotNetTools { get; set; }

		/// <summary>
		/// Used to generate Markdown documentation from XML comments.
		/// </summary>
		public DotNetDocsSettings DocsSettings { get; set; }

		/// <summary>
		/// The version of the <c>sourcelink</c> tool to use when testing packages.
		/// </summary>
		/// <remarks>Defaults to a stable version, which may change with new versions of <b>Faithlife.Build</b>,
		/// but will not change unless <b>Faithlife.Build</b> is updated.</remarks>
		public string SourceLinkToolVersion { get; set; }

		/// <summary>
		/// The options and flags used by <see cref="DotNetBuild"/>.
		/// </summary>
		/// <remarks>Any properties not set before <see cref="DotNetBuild.AddDotNetTargets"/> is called
		/// will be set afterward.</remarks>
		public DotNetBuildOptions BuildOptions { get; set; }
	}
}
