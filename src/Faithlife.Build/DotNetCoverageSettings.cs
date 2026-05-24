namespace Faithlife.Build;

/// <summary>
/// Settings for running .NET tests with coverage.
/// </summary>
public sealed class DotNetCoverageSettings
{
	/// <summary>
	/// Called to find the projects to test with coverage. If unspecified, test projects under <c>tests/**/*.csproj</c> are used.
	/// </summary>
	public Func<DotNetBuildSettings, IReadOnlyList<string>>? FindProjects { get; set; }

	/// <summary>
	/// The target framework to test with coverage. If unspecified, no target framework is passed to <c>dotnet test</c>.
	/// </summary>
	public string? TargetFramework { get; set; }

	/// <summary>
	/// Clones the settings.
	/// </summary>
	public DotNetCoverageSettings Clone() => (DotNetCoverageSettings) MemberwiseClone();
}
