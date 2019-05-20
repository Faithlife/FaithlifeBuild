using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

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
			string args = EscapeAndConcatenate(settings.Arguments ?? Enumerable.Empty<string>());

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

		// copied from https://github.com/natemcmaster/CommandLineUtils/blob/master/src/CommandLineUtils/Utilities/ArgumentEscaper.cs
		// but also ignores null, escapes empty
		private static string EscapeAndConcatenate(IEnumerable<string> args) => string.Join(" ", args.Where(x => x != null).Select(EscapeSingleArg));

		private static string EscapeSingleArg(string arg)
		{
			var sb = new StringBuilder();

			var needsQuotes = arg.Length == 0 || ContainsWhitespace(arg);
			var isQuoted = needsQuotes || IsSurroundedWithQuotes(arg);

			if (needsQuotes)
			{
				sb.Append('"');
			}

			for (int i = 0; i < arg.Length; ++i)
			{
				var backslashes = 0;

				// Consume all backslashes
				while (i < arg.Length && arg[i] == '\\')
				{
					backslashes++;
					i++;
				}

				if (i == arg.Length && isQuoted)
				{
					// Escape any backslashes at the end of the arg when the argument is also quoted.
					// This ensures the outside quote is interpreted as an argument delimiter
					sb.Append('\\', 2 * backslashes);
				}
				else if (i == arg.Length)
				{
					// At then end of the arg, which isn't quoted,
					// just add the backslashes, no need to escape
					sb.Append('\\', backslashes);
				}
				else if (arg[i] == '"')
				{
					// Escape any preceding backslashes and the quote
					sb.Append('\\', (2 * backslashes) + 1);
					sb.Append('"');
				}
				else
				{
					// Output any consumed backslashes and the character
					sb.Append('\\', backslashes);
					sb.Append(arg[i]);
				}
			}

			if (needsQuotes)
			{
				sb.Append('"');
			}

			return sb.ToString();
		}

		private static bool IsSurroundedWithQuotes(string argument)
		{
			if (argument.Length <= 1)
			{
				return false;
			}

			return argument[0] == '"' && argument[argument.Length - 1] == '"';
		}

		private static bool ContainsWhitespace(string argument) => argument.IndexOfAny(new[] { ' ', '\t', '\n' }) >= 0;
	}
}
