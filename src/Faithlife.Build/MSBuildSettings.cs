namespace Faithlife.Build
{
	/// <summary>
	/// The settings to use when running MSBuild.
	/// </summary>
	public sealed class MSBuildSettings
	{
		/// <summary>
		/// Specifies the <c>MSBuild</c> version to use.
		/// </summary>
		public MSBuildVersion? Version { get; set; }

		/// <summary>
		/// Specifies the <c>MSBuild</c> platform to use.
		/// </summary>
		public MSBuildPlatform? Platform { get; set; }

		/// <summary>
		/// The path of MSBuild. Used to override the default.
		/// </summary>
		public string? MSBuildPath { get; set; }

		/// <summary>
		/// Clones the settings.
		/// </summary>
		public MSBuildSettings Clone() => (MSBuildSettings) MemberwiseClone();
	}
}
