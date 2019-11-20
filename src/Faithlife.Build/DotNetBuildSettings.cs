using System;
using System.Collections.Generic;

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
		public string? SolutionName { get; set; }

		/// <summary>
		/// The NuGet API key with which to push packages.
		/// </summary>
		public string? NuGetApiKey { get; set; }

		/// <summary>
		/// The NuGet source to which to push packages. The standard public NuGet source if omitted.
		/// </summary>
		public string? NuGetSource { get; set; }

		/// <summary>
		/// Used to install .NET Core Global tools.
		/// </summary>
		/// <remarks>Optional. If not specified, installs tools under <c>tools/bin</c></remarks>
		public DotNetTools? DotNetTools { get; set; }

		/// <summary>
		/// Used to generate Markdown documentation from XML comments.
		/// </summary>
		public DotNetDocsSettings? DocsSettings { get; set; }

		/// <summary>
		/// The options and flags used by <see cref="DotNetBuild"/>.
		/// </summary>
		/// <remarks>Any properties not set before <see cref="DotNetBuild.AddDotNetTargets"/> is called
		/// will be set afterward.</remarks>
		public DotNetBuildOptions? BuildOptions { get; set; }

		/// <summary>
		/// Set to use <c>MSBuild</c> instead of <c>dotnet</c> to build the solution.
		/// </summary>
		public MSBuildSettings? MSBuildSettings { get; set; }

		/// <summary>
		/// Settings for running unit tests.
		/// </summary>
		public DotNetTestSettings? TestSettings { get; set; }

		/// <summary>
		/// Settings for creating NuGet packages.
		/// </summary>
		public DotNetPackageSettings? PackageSettings { get; set; }

		/// <summary>
		/// Settings for cleaning projects.
		/// </summary>
		public DotNetCleanSettings? CleanSettings { get; set; }

		/// <summary>
		/// The default solution platform to build (optional).
		/// </summary>
		public string? SolutionPlatform { get; set; }

		/// <summary>
		/// The maximum number of CPUs to use when building.
		/// </summary>
		/// <remarks>Use <c>1</c> to enforce sequential builds.</remarks>
		public int? MaxCpuCount { get; set; }

		/// <summary>
		/// A function that returns any extra properties for the specified build target.
		/// </summary>
		/// <remarks>Supported build targets include <c>clean</c>, <c>restore</c>, <c>build</c>, <c>test</c>,
		/// and <c>package</c>.</remarks>
		public Func<string, IEnumerable<(string Key, string Value)>>? ExtraProperties { get; set; }

		/// <summary>
		/// The SourceLink settings. Must be set to test SourceLink URLs.
		/// </summary>
		public SourceLinkSettings? SourceLinkSettings { get; set; }

		/// <summary>
		/// Clones the settings.
		/// </summary>
		public DotNetBuildSettings Clone()
		{
			var clone = (DotNetBuildSettings) MemberwiseClone();
			clone.DocsSettings = clone.DocsSettings?.Clone();
			clone.MSBuildSettings = clone.MSBuildSettings?.Clone();
			clone.TestSettings = clone.TestSettings?.Clone();
			clone.PackageSettings = clone.PackageSettings?.Clone();
			clone.CleanSettings = clone.CleanSettings?.Clone();
			clone.SourceLinkSettings = clone.SourceLinkSettings?.Clone();
			return clone;
		}
	}
}
