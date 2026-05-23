namespace Faithlife.Build;

/// <summary>
/// Settings for running .NET tests with coverage.
/// </summary>
public sealed class DotNetCoverageSettings
{
	/// <summary>
	/// Called to find the projects to test with coverage.
	/// </summary>
	public Func<DotNetBuildSettings, IReadOnlyList<string>>? FindProjects { get; set; }

	/// <summary>
	/// The target framework to test with coverage.
	/// </summary>
	public string? TargetFramework { get; set; }

	/// <summary>
	/// The run settings file to use when testing with coverage.
	/// </summary>
	public string? RunSettingsPath { get; set; }

	/// <summary>
	/// The directory for coverage test results.
	/// </summary>
	public string? TestResultsDirectory { get; set; }

	/// <summary>
	/// The directory for generated coverage reports.
	/// </summary>
	public string? ReportDirectory { get; set; }

	/// <summary>
	/// The ReportGenerator assembly filters.
	/// </summary>
	public IReadOnlyList<string>? AssemblyFilters { get; set; }

	/// <summary>
	/// The ReportGenerator report types.
	/// </summary>
	public IReadOnlyList<string>? ReportTypes { get; set; }

	/// <summary>
	/// Clones the settings.
	/// </summary>
	public DotNetCoverageSettings Clone() => (DotNetCoverageSettings) MemberwiseClone();
}
