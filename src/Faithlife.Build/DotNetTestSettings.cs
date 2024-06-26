namespace Faithlife.Build;

/// <summary>
/// Settings for running .NET tests.
/// </summary>
public sealed class DotNetTestSettings
{
	/// <summary>
	/// Called to find the projects to test.
	/// </summary>
	public Func<IReadOnlyList<string>>? FindProjects { get; set; }

	/// <summary>
	/// Called to find the test assemblies.
	/// </summary>
	public Func<DotNetBuildSettings, IReadOnlyList<string>>? FindAssemblies { get; set; }

	/// <summary>
	/// Called to find the test assemblies.
	/// </summary>
	[Obsolete("Use FindAssemblies.")]
	public Func<IReadOnlyList<string>>? FindTestAssemblies { get; set; }

	/// <summary>
	/// Called to run tests on the specified solution, project, or assembly.
	/// </summary>
	public Action<string?>? RunTests { get; set; }

	/// <summary>
	/// True to run tests on multiple projects or assemblies in parallel.
	/// </summary>
	public bool? UseParallel { get; set; }

	/// <summary>
	/// Get the logger for the specified project.
	/// </summary>
	public Func<string?, string?>? GetLogger { get; set; }

	/// <summary>
	/// Clones the settings.
	/// </summary>
	public DotNetTestSettings Clone() => (DotNetTestSettings) MemberwiseClone();
}
