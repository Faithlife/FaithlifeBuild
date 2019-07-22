using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using McMaster.Extensions.CommandLineUtils;

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
		public static void RunApp(string path, params string[] args) => RunApp(path, args.AsEnumerable());

		/// <summary>
		/// Runs the specified command-line app.
		/// </summary>
		/// <param name="path">The path of the command-line app.</param>
		/// <param name="args">The arguments to send to the command-line app.</param>
		public static void RunApp(string path, IEnumerable<string> args) => RunApp(path, new AppRunnerSettings { Arguments = args });

		/// <summary>
		/// Runs the specified command-line app.
		/// </summary>
		/// <param name="path">The path of the command-line app.</param>
		/// <param name="settings">The settings to use when running the app.</param>
		public static int RunApp(string path, AppRunnerSettings settings)
		{
			// adapted from https://github.com/adamralph/simple-exec; allows non-exceptional non-zero edit code
			string args = ArgumentEscaper.EscapeAndConcatenate((settings.Arguments ?? Enumerable.Empty<string>()).Where(x => x != null));

			var startInfo = new ProcessStartInfo
			{
				WorkingDirectory = settings.WorkingDirectory,
				UseShellExecute = false,
				RedirectStandardError = false,
				RedirectStandardOutput = false,
			};

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				startInfo.FileName = "cmd.exe";
				startInfo.Arguments = $"/c \"\"{path}\" {args}\"";
			}
			else
			{
				startInfo.FileName = path;
				startInfo.Arguments = args;
			}

			if (!settings.NoEcho)
			{
				if (startInfo.WorkingDirectory.Length != 0)
					Console.Error.WriteLine($"Working directory: {startInfo.WorkingDirectory}");
				Console.Error.WriteLine($"{startInfo.FileName} {startInfo.Arguments}");
			}

			using (var process = new Process { StartInfo = startInfo })
			{
				process.Start();
				process.WaitForExit();

				int exitCode = process.ExitCode;

				var isExitCodeSuccess = settings.IsExitCodeSuccess ?? (x => x == 0);
				if (!isExitCodeSuccess(exitCode))
					throw new ApplicationException($"The process failed with exit code {exitCode}.");

				return exitCode;
			}
		}

		/// <summary>
		/// Runs the specified .NET Framework command-line app.
		/// </summary>
		/// <param name="path">The path of the command-line app.</param>
		/// <param name="args">The arguments to send to the command-line app.</param>
		/// <remarks>On Linux and macOS, Mono is used to run the app.</remarks>
		public static void RunDotNetFrameworkApp(string path, params string[] args) => RunDotNetFrameworkApp(path, args.AsEnumerable());

		/// <summary>
		/// Runs the specified .NET Framework command-line app.
		/// </summary>
		/// <param name="path">The path of the command-line app.</param>
		/// <param name="args">The arguments to send to the command-line app.</param>
		/// <remarks>On Linux and macOS, Mono is used to run the app.</remarks>
		public static void RunDotNetFrameworkApp(string path, IEnumerable<string> args) => RunDotNetFrameworkApp(path, new AppRunnerSettings { Arguments = args });

		/// <summary>
		/// Runs the specified .NET Framework command-line app.
		/// </summary>
		/// <param name="path">The path of the command-line app.</param>
		/// <param name="settings">The settings to use when running the app.</param>
		/// <remarks>On Linux and macOS, Mono is used to run the app.</remarks>
		public static int RunDotNetFrameworkApp(string path, AppRunnerSettings settings)
		{
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
