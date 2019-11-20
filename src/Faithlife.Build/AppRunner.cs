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
		public static void RunApp(string path, params string?[] args)
		{
			if (args == null)
				throw new ArgumentNullException(nameof(args));

			RunApp(path, args.AsEnumerable());
		}

		/// <summary>
		/// Runs the specified command-line app.
		/// </summary>
		/// <param name="path">The path of the command-line app.</param>
		/// <param name="args">The arguments to send to the command-line app.</param>
		public static void RunApp(string path, IEnumerable<string?> args)
		{
			if (args == null)
				throw new ArgumentNullException(nameof(args));

			RunApp(path, new AppRunnerSettings { Arguments = args });
		}

		/// <summary>
		/// Runs the specified command-line app.
		/// </summary>
		/// <param name="path">The path of the command-line app.</param>
		/// <param name="settings">The settings to use when running the app.</param>
		public static int RunApp(string path, AppRunnerSettings settings)
		{
			if (path == null)
				throw new ArgumentNullException(nameof(path));
			if (settings == null)
				throw new ArgumentNullException(nameof(settings));

			var args = ArgumentEscaper.EscapeAndConcatenate((settings.Arguments ?? Enumerable.Empty<string>()).Where(x => x != null));

			var exitCode = 0;
			try
			{
				Command.Run(name: path, args: args, workingDirectory: settings.WorkingDirectory, noEcho: settings.NoEcho);
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

		/// <summary>
		/// Runs the specified .NET Framework command-line app.
		/// </summary>
		/// <param name="path">The path of the command-line app.</param>
		/// <param name="args">The arguments to send to the command-line app.</param>
		/// <remarks>On Linux and macOS, Mono is used to run the app.</remarks>
		public static void RunDotNetFrameworkApp(string path, params string[] args)
		{
			if (args == null)
				throw new ArgumentNullException(nameof(args));

			RunDotNetFrameworkApp(path, args.AsEnumerable());
		}

		/// <summary>
		/// Runs the specified .NET Framework command-line app.
		/// </summary>
		/// <param name="path">The path of the command-line app.</param>
		/// <param name="args">The arguments to send to the command-line app.</param>
		/// <remarks>On Linux and macOS, Mono is used to run the app.</remarks>
		public static void RunDotNetFrameworkApp(string path, IEnumerable<string> args)
		{
			if (args == null)
				throw new ArgumentNullException(nameof(args));

			RunDotNetFrameworkApp(path, new AppRunnerSettings { Arguments = args });
		}

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
	}
}
