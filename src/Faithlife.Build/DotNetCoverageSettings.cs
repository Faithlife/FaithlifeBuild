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
	/// The run settings file to use when testing with coverage. If unspecified, <c>coverage.runsettings</c> is used when it exists.
	/// </summary>
	public string? RunSettingsPath { get; set; }

	/// <summary>
	/// The directory for coverage test results. If unspecified, <c>artifacts/Coverage/TestResults</c> is used.
	/// </summary>
	public string? TestResultsDirectory { get; set; }

	/// <summary>
	/// The directory for generated coverage reports. If unspecified, <c>artifacts/Coverage/Report</c> is used.
	/// </summary>
	public string? ReportDirectory { get; set; }

	/// <summary>
	/// The ReportGenerator assembly filters. If unspecified, no assembly filters are passed to ReportGenerator.
	/// </summary>
	public IReadOnlyList<string>? AssemblyFilters { get; set; }

	/// <summary>
	/// The ReportGenerator report types. If unspecified, HTML, Cobertura, text summary, and Markdown assemblies summary reports are generated.
	/// </summary>
	public IReadOnlyList<string>? ReportTypes { get; set; }

	/// <summary>
	/// Clones the settings.
	/// </summary>
	public DotNetCoverageSettings Clone() => (DotNetCoverageSettings) MemberwiseClone();
}
