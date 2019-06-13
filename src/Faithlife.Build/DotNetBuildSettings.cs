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
		public string SolutionName { get; set; }

		/// <summary>
		/// The NuGet API key with which to push packages.
		/// </summary>
		public string NuGetApiKey { get; set; }

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

		/// <summary>
		/// A function that returns true if the named project uses SourceLink.
		/// </summary>
		/// <remarks>If not specified, all projects are assumed to use SourceLink. This property
		/// determines whether the <c>sourcelink</c> tool is used to test each package.</remarks>
		public Func<string, bool> ProjectUsesSourceLink { get; set; }

		/// <summary>
		/// Set to use <c>MSBuild</c> instead of <c>dotnet</c> to build the solution.
		/// </summary>
		public MSBuildSettings MSBuildSettings { get; set; }

		/// <summary>
		/// Settings for running unit tests.
		/// </summary>
		public DotNetTestSettings TestSettings { get; set; }

		/// <summary>
		/// The default solution platform to build (optional).
		/// </summary>
		public string SolutionPlatform { get; set; }

		/// <summary>
		/// A function that returns true if the named project uses semantic versioning.
		/// </summary>
		/// <remarks>If not specified, all projects are assumed to use semantic versioning. This property
		/// determines whether the <c>packagediff</c> tool is used to test each package.</remarks>
		public Func<string, bool> ProjectUsesSemVer { get; set; }

		/// <summary>
		/// The version of the <c>packagediff</c> tool to use when testing packages.
		/// </summary>
		/// <remarks>Defaults to a stable version, which may change with new versions of <b>Faithlife.Build</b>,
		/// but will not change unless <b>Faithlife.Build</b> is updated.</remarks>
		public string PackageDiffToolVersion { get; set; }

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
		public Func<string, IEnumerable<(string Key, string Value)>> ExtraProperties { get; set; }

		/// <summary>
		/// Clones the settings.
		/// </summary>
		public DotNetBuildSettings Clone()
		{
			var clone = (DotNetBuildSettings) MemberwiseClone();
			clone.DocsSettings = clone.DocsSettings?.Clone();
			clone.MSBuildSettings = clone.MSBuildSettings?.Clone();
			clone.TestSettings = clone.TestSettings?.Clone();
			return clone;
		}
	}
}
