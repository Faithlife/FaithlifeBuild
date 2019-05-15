using System.Collections.Generic;
using System.Linq;
using McMaster.Extensions.CommandLineUtils;
using static Faithlife.Build.AppRunner;

namespace Faithlife.Build
{
	/// <summary>
	/// Runs <c>dotnet</c> commands.
	/// </summary>
	/// <remarks>
	/// <para>Consider calling these methods directly via <c>using static Faithlife.Build.DotNetRunner;</c></para>
	/// </remarks>
	public static class DotNetRunner
	{
		/// <summary>
		/// Runs <c>dotnet</c> with the specified arguments.
		/// </summary>
		/// <param name="args">The command-line arguments.</param>
		public static void RunDotNet(params string[] args) => RunDotNet(args.AsEnumerable());

		/// <summary>
		/// Runs <c>dotnet</c> with the specified arguments.
		/// </summary>
		/// <param name="args">The command-line arguments.</param>
		public static void RunDotNet(IEnumerable<string> args) => RunApp(DotNetExe.FullPath, args);

		/// <summary>
		/// Runs <c>dotnet</c> with the specified settings.
		/// </summary>
		/// <param name="settings">The settings to use when running the app.</param>
		public static void RunDotNet(AppRunnerSettings settings) => RunApp(DotNetExe.FullPath, settings);
	}
}
