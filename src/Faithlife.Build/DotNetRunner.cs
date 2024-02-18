using McMaster.Extensions.CommandLineUtils;
using static Faithlife.Build.AppRunner;

namespace Faithlife.Build;

/// <summary>
/// Runs <c>dotnet</c> commands.
/// </summary>
/// <remarks>
/// <para>Consider calling these methods directly via <c>using static Faithlife.Build.DotNetRunner;</c>.</para>
/// </remarks>
public static class DotNetRunner
{
	/// <summary>
	/// Runs <c>dotnet</c> with the specified arguments.
	/// </summary>
	/// <param name="args">The command-line arguments.</param>
	public static void RunDotNet(params string?[] args) =>
		RunDotNet((args ?? throw new ArgumentNullException(nameof(args))).AsEnumerable());

	/// <summary>
	/// Runs <c>dotnet</c> with the specified arguments.
	/// </summary>
	/// <param name="args">The command-line arguments.</param>
	public static void RunDotNet(IEnumerable<string?> args) =>
		RunApp(GetDotNetFullPath(), args);

	/// <summary>
	/// Runs <c>dotnet</c> with the specified settings.
	/// </summary>
	/// <param name="settings">The settings to use when running the app.</param>
	public static int RunDotNet(AppRunnerSettings settings) =>
		RunApp(GetDotNetFullPath(), settings);

	/// <summary>
	/// Runs the specified .NET tool with the specified arguments.
	/// </summary>
	/// <param name="name">The name (or path) of the tool.</param>
	/// <param name="args">The command-line arguments.</param>
	public static void RunDotNetTool(string name, params string?[] args) =>
		RunDotNetTool(name, (args ?? throw new ArgumentNullException(nameof(args))).AsEnumerable());

	/// <summary>
	/// Runs the specified .NET tool with the specified arguments.
	/// </summary>
	/// <param name="name">The name (or path) of the tool.</param>
	/// <param name="args">The command-line arguments.</param>
	public static void RunDotNetTool(string name, IEnumerable<string?> args) =>
		RunDotNetTool(name, new AppRunnerSettings { Arguments = args ?? throw new ArgumentNullException(nameof(args)) });

	/// <summary>
	/// Runs the specified .NET tool with the specified settings.
	/// </summary>
	/// <param name="name">The name (or path) of the tool.</param>
	/// <param name="settings">The settings to use when running the app.</param>
	public static int RunDotNetTool(string name, AppRunnerSettings settings)
	{
		settings = (settings ?? throw new ArgumentNullException(nameof(settings))).Clone();
		settings.Arguments = ["tool", "run", name, "--", .. settings.Arguments ?? []];
		return RunDotNet(settings);
	}

	private static string GetDotNetFullPath() => DotNetExe.FullPathOrDefault();
}
