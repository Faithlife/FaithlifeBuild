using System.Collections;
using System.Xml.Linq;
using System.Xml.XPath;
using NuGet.Versioning;
using static Faithlife.Build.AppRunner;

namespace Faithlife.Build;

/// <summary>
/// Provides access to a classic NuGet packaged tool.
/// </summary>
public sealed class DotNetClassicTool
{
	/// <summary>
	/// Accesses a classic NuGet packaged tool using the standard Build project.
	/// </summary>
	/// <param name="packageName">The name of the NuGet package.</param>
	/// <param name="toolName">The name of the tool executable, as found in the <c>tools</c> folder of the NuGet package. Defaults to the package name.</param>
	/// <exception cref="BuildException">The tool is not installed.</exception>
	/// <remarks>The version of the tool is determined by the matching <c>PackageReference</c> in <c>tools/Build/Build.csproj</c>.</remarks>
	public static DotNetClassicTool Create(string packageName, string? toolName = null) => CreateFrom(GetDefaultProjectPath(), packageName, toolName);

	/// <summary>
	/// Accesses a classic NuGet packaged tool using the standard Build project.
	/// </summary>
	/// <param name="packageName">The name of the NuGet package.</param>
	/// <param name="toolName">The name of the tool executable, as found in the <c>tools</c> folder of the NuGet package. Defaults to the package name.</param>
	/// <returns>Null if the tool is not installed.</returns>
	/// <remarks>The version of the tool is determined by the matching <c>PackageReference</c> in <c>tools/Build/Build.csproj</c>.</remarks>
	public static DotNetClassicTool? TryCreate(string packageName, string? toolName = null) => TryCreateFrom(GetDefaultProjectPath(), packageName, toolName);

	/// <summary>
	/// Accesses a classic NuGet packaged tool using the specified project.
	/// </summary>
	/// <param name="projectPath">The C# project file path containing the NuGet package version.</param>
	/// <param name="packageName">The name of the NuGet package.</param>
	/// <param name="toolName">The name of the tool executable, as found in the <c>tools</c> folder of the NuGet package. Defaults to the package name.</param>
	/// <exception cref="BuildException">The tool is not installed.</exception>
	public static DotNetClassicTool CreateFrom(string projectPath, string packageName, string? toolName = null) =>
		TryCreateFrom(projectPath, packageName, toolName) ?? throw new BuildException($"Tool '{toolName ?? packageName}' from '{packageName}' is not installed.");

	/// <summary>
	/// Accesses a classic NuGet packaged tool using the specified project.
	/// </summary>
	/// <param name="projectPath">The C# project file path containing the NuGet package version.</param>
	/// <param name="packageName">The name of the NuGet package.</param>
	/// <param name="toolName">The name of the tool executable, as found in the <c>tools</c> folder of the NuGet package. Defaults to the package name.</param>
	/// <returns>Null if the tool is not installed.</returns>
	public static DotNetClassicTool? TryCreateFrom(string projectPath, string packageName, string? toolName = null)
	{
		if (!File.Exists(projectPath))
			throw new BuildException($"Missing project file: {projectPath}");

		var packageVersion = ((IEnumerable) XDocument.Load(projectPath).XPathEvaluate("//PackageReference"))
			.OfType<XElement>()
			.Where(x => string.Equals(x.Attribute("Include")?.Value, packageName, StringComparison.OrdinalIgnoreCase))
			.Select(x => x.Attribute("Version")?.Value)
			.FirstOrDefault();
		if (packageVersion is null)
			return null;

		var packagesPath = Environment.GetEnvironmentVariable("NUGET_PACKAGES") ??
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");

		// Find a matching package. If using a specific version, there will be exactly one match.
		// If using floating versions, there may be multiple matches, so take the latest semantic version.
		var packagePath = Directory.GetDirectories(Path.Combine(packagesPath, packageName.ToLowerInvariant()), packageVersion)
			.OrderByDescending(fullPath => NuGetVersion.Parse(Path.GetFileName(fullPath)))
			.FirstOrDefault();

		if (!Directory.Exists(packagePath))
			throw new BuildException($"Missing restored NuGet package: {packagePath}");
		Console.WriteLine(packagePath);

		return new DotNetClassicTool(Path.Combine(packagePath, "tools", toolName ?? packageName));
	}

	/// <summary>
	/// The path of the tool executable.
	/// </summary>
	public string ToolPath { get; }

	/// <summary>
	/// Runs the tool with the specified arguments.
	/// </summary>
	/// <param name="args">The command-line arguments.</param>
	public void Run(params string?[] args) => RunApp(ToolPath, args);

	/// <summary>
	/// Runs the tool with the specified arguments.
	/// </summary>
	/// <param name="args">The command-line arguments.</param>
	public void Run(IEnumerable<string?> args) => RunApp(ToolPath, args);

	/// <summary>
	/// Runs the tool with the specified settings.
	/// </summary>
	/// <param name="settings">The settings to use when running the tool.</param>
	public int Run(AppRunnerSettings settings) => RunApp(ToolPath, settings);

	private DotNetClassicTool(string toolPath) => ToolPath = toolPath;

	private static string GetDefaultProjectPath() => Path.Combine("tools", "Build", "Build.csproj");
}
