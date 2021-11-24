namespace Faithlife.Build;

/// <summary>
/// Information about a NuGet package.
/// </summary>
public sealed class DotNetPackageInfo
{
	/// <summary>
	/// The name of the package.
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// The version of the package, including the suffix.
	/// </summary>
	public string Version { get; }

	internal DotNetPackageInfo(string name, string version, string suffix)
	{
		Name = name;
		Version = version;
		Suffix = suffix;
	}

	internal string Suffix { get; }
}
