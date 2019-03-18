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
		/// <param name="args">The arguments, if any.</param>
		public static void RunDotNet(params string[] args) => RunApp(DotNetExe.FullPath, args);
	}
}
