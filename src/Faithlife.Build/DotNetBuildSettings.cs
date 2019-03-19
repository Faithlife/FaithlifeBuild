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
		public DocumentationSettings DocumentationSettings { get; set; }
	}
}
