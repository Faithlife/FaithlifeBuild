using System.Linq;
using McMaster.Extensions.CommandLineUtils;
using SimpleExec;

namespace Faithlife.Build
{
	/// <summary>
	/// Runs command-line apps.
	/// </summary>
	/// <remarks>
	/// <para>Consider calling these methods directly via <c>using static Faithlife.Build.AppRunner;</c></para>
	/// </remarks>
	public static class AppRunner
	{
		/// <summary>
		/// Runs the specified command-line app.
		/// </summary>
		/// <param name="path">The path of the command-line app.</param>
		/// <param name="args">The arguments to send to the command-line app.</param>
		public static void RunApp(string path, params string[] args) =>
			Command.Run(path, ArgumentEscaper.EscapeAndConcatenate(args.Where(x => x != null)));

		/// <summary>
		/// Runs the specified .NET Framework command-line app.
		/// </summary>
		/// <param name="path">The path of the command-line app.</param>
		/// <param name="args">The arguments to send to the command-line app.</param>
		/// <remarks>On Linux and macOS, Mono is used to run the app.</remarks>
		public static void RunDotNetFrameworkApp(string path, params string[] args)
		{
			if (BuildEnvironment.IsUnix())
			{
				args = new[] { path }.Concat(args).ToArray();
				path = "mono";
			}

			RunApp(path, args);
		}
	}
}
