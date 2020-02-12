using System;
using System.Collections.Generic;
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
		public static void RunApp(string path, params string?[] args) =>
			RunApp(path, args?.AsEnumerable() ?? throw new ArgumentNullException(nameof(args)));

		/// <summary>
		/// Runs the specified command-line app.
		/// </summary>
		/// <param name="path">The path of the command-line app.</param>
		/// <param name="args">The arguments to send to the command-line app.</param>
		public static void RunApp(string path, IEnumerable<string?> args) =>
			RunApp(path, new AppRunnerSettings { Arguments = args ?? throw new ArgumentNullException(nameof(args)) });

		/// <summary>
		/// Runs the specified command-line app.
		/// </summary>
		/// <param name="path">The path of the command-line app.</param>
		/// <param name="settings">The settings to use when running the app.</param>
		public static int RunApp(string path, AppRunnerSettings settings) =>
			DoRunApp(path, settings, useCmdOnWindows: false);

		/// <summary>
		/// Runs the specified .NET Framework command-line app.
		/// </summary>
		/// <param name="path">The path of the command-line app.</param>
		/// <param name="args">The arguments to send to the command-line app.</param>
		/// <remarks>On Linux and macOS, Mono is used to run the app.</remarks>
		public static void RunDotNetFrameworkApp(string path, params string?[] args) =>
			RunDotNetFrameworkApp(path, args?.AsEnumerable() ?? throw new ArgumentNullException(nameof(args)));

		/// <summary>
		/// Runs the specified .NET Framework command-line app.
		/// </summary>
		/// <param name="path">The path of the command-line app.</param>
		/// <param name="args">The arguments to send to the command-line app.</param>
		/// <remarks>On Linux and macOS, Mono is used to run the app.</remarks>
		public static void RunDotNetFrameworkApp(string path, IEnumerable<string?> args) =>
			RunDotNetFrameworkApp(path, new AppRunnerSettings { Arguments = args ?? throw new ArgumentNullException(nameof(args)) });

		/// <summary>
		/// Runs the specified .NET Framework command-line app.
		/// </summary>
		/// <param name="path">The path of the command-line app.</param>
		/// <param name="settings">The settings to use when running the app.</param>
		/// <remarks>On Linux and macOS, Mono is used to run the app.</remarks>
		public static int RunDotNetFrameworkApp(string path, AppRunnerSettings settings)
		{
			if (path == null)
				throw new ArgumentNullException(nameof(path));
			if (settings == null)
				throw new ArgumentNullException(nameof(settings));

			if (BuildEnvironment.IsUnix())
			{
				settings = settings.Clone();
				settings.Arguments = new[] { path }.Concat(settings.Arguments ?? Enumerable.Empty<string>()).ToList();
				path = "mono";
			}

			return RunApp(path, settings);
		}

		/// <summary>
		/// Runs the specified command-line app, via <c>cmd /c</c> on Windows.
		/// </summary>
		/// <param name="path">The path of the command-line app.</param>
		/// <param name="args">The arguments to send to the command-line app.</param>
		public static void RunCmd(string path, params string?[] args) =>
			RunCmd(path, args?.AsEnumerable() ?? throw new ArgumentNullException(nameof(args)));

		/// <summary>
		/// Runs the specified command-line app, via <c>cmd /c</c> on Windows.
		/// </summary>
		/// <param name="path">The path of the command-line app.</param>
		/// <param name="args">The arguments to send to the command-line app.</param>
		public static void RunCmd(string path, IEnumerable<string?> args) =>
			RunCmd(path, new AppRunnerSettings { Arguments = args ?? throw new ArgumentNullException(nameof(args)) });

		/// <summary>
		/// Runs the specified command-line app, via <c>cmd /c</c> on Windows.
		/// </summary>
		/// <param name="path">The path of the command-line app.</param>
		/// <param name="settings">The settings to use when running the app.</param>
		public static int RunCmd(string path, AppRunnerSettings settings) =>
			DoRunApp(path, settings, useCmdOnWindows: true);

		private static int DoRunApp(string path, AppRunnerSettings settings, bool useCmdOnWindows)
		{
			if (path == null)
				throw new ArgumentNullException(nameof(path));
			if (settings == null)
				throw new ArgumentNullException(nameof(settings));

			var arguments = (settings.Arguments ?? Enumerable.Empty<string>()).Where(x => x != null);
			string argsString;
			if (useCmdOnWindows && BuildEnvironment.IsWindows())
			{
				argsString = "/S /C \"" + ArgumentEscaper.EscapeAndConcatenate(arguments.Prepend(path)) + "\"";
				path = "cmd.exe";
			}
			else
			{
				argsString = ArgumentEscaper.EscapeAndConcatenate(arguments);
			}

			var exitCode = 0;
			try
			{
				Command.Run(name: path, args: argsString, workingDirectory: settings.WorkingDirectory, noEcho: settings.NoEcho);
			}
			catch (NonZeroExitCodeException exception)
			{
				exitCode = exception.ExitCode;
			}

			var isExitCodeSuccess = settings.IsExitCodeSuccess ?? (x => x == 0);
			if (!isExitCodeSuccess(exitCode))
				throw new ApplicationException($"The process failed with exit code {exitCode}.");

			return exitCode;
		}
	}
}
