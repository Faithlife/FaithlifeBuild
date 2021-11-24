namespace Faithlife.Build
{
	/// <summary>
	/// The settings to use when using SourceLink.
	/// </summary>
	[Obsolete("Support for sourcelink test was removed.")]
	public sealed class SourceLinkSettings
	{
		/// <summary>
		/// The version of the <c>sourcelink</c> tool to use when testing packages.
		/// </summary>
		/// <remarks>Defaults to a stable version, which may change with new versions of <b>Faithlife.Build</b>,
		/// but will not change unless <b>Faithlife.Build</b> is updated.</remarks>
		public string? ToolVersion { get; set; }

		/// <summary>
		/// A function that returns true if SourceLink URLs should be tested for the package with the specified name.
		/// </summary>
		/// <remarks>If not specified, all packages will be tested.</remarks>
		public Func<string, bool>? ShouldTestPackage { get; set; }

		/// <summary>
		/// The username used with Basic authentication when testing SourceLink URLs (optional).
		/// </summary>
		public string? Username { get; set; }

		/// <summary>
		/// The password used with Basic authentication when testing SourceLink URLs (optional).
		/// </summary>
		public string? Password { get; set; }

		/// <summary>
		/// Returns the default settings.
		/// </summary>
		public static SourceLinkSettings Default => new SourceLinkSettings();

		/// <summary>
		/// Clones the settings.
		/// </summary>
		public SourceLinkSettings Clone() => (SourceLinkSettings) MemberwiseClone();
	}
}
