namespace Faithlife.Build;

/// <summary>
/// Settings for creating and publishing NuGet packages.
/// </summary>
public sealed class DotNetPackageSettings
{
	/// <summary>
	/// Settings for signing NuGet packages.
	/// </summary>
	public DotNetSigningSettings? SigningSettings { get; set; }

	/// <summary>
	/// Called to find the projects to package.
	/// </summary>
	public Func<IReadOnlyList<string>>? FindProjects { get; set; }

	/// <summary>
	/// Set if a git tag should be pushed when a NuGet package is published.
	/// </summary>
	/// <remarks>The delegate calculates the name of the tag from the package
	/// information, e.g. <c>x => $"nuget.{x.Version}"</c>.</remarks>
	public Func<DotNetPackageInfo, string?>? PushTagOnPublish { get; set; }

	/// <summary>
	/// Credentials used to push tags to git.
	/// </summary>
	public GitLoginInfo? GitLogin { get; set; }

	/// <summary>
	/// The URL of the git repository where tags are pushed.
	/// </summary>
	public string? GitRepositoryUrl { get; set; }

	/// <summary>
	/// Clones the settings.
	/// </summary>
	public DotNetPackageSettings Clone() => (DotNetPackageSettings) MemberwiseClone();
}
