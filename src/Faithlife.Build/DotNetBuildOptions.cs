namespace Faithlife.Build
{
	/// <summary>
	/// The options and flags used by <see cref="DotNetBuild"/>.
	/// </summary>
	public sealed class DotNetBuildOptions
	{
		/// <summary>
		/// The configuration to build.
		/// </summary>
		public BuildOption ConfigurationOption { get; set; }

		/// <summary>
		/// NuGet API key for publishing.
		/// </summary>
		public BuildOption NuGetApiKeyOption { get; set; }

		/// <summary>
		/// Generates a prerelease package.
		/// </summary>
		public BuildOption VersionSuffixOption { get; set; }

		/// <summary>
		/// Directory for generated package.
		/// </summary>
		public BuildOption NuGetOutputOption { get; set; }

		/// <summary>
		/// The git branch or tag that triggered the build.
		/// </summary>
		public BuildOption TriggerOption { get; set; }

		/// <summary>
		/// The git branch being built.
		/// </summary>
		public BuildOption BranchOption { get; set; }
	}
}
