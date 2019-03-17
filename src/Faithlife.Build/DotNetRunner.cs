using System;
using System.IO;
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
		/// Sets the target directory for any locally-installed .NET Core tools.
		/// </summary>
		/// <param name="path">The target directory.</param>
		public static void SetDotNetToolsDirectory(string path) => s_toolsDirectory = path;

		/// <summary>
		/// Runs <c>dotnet</c> with the specified arguments.
		/// </summary>
		/// <param name="args">The arguments, if any.</param>
		public static void RunDotNet(params string[] args) => RunApp(DotNetExe.FullPath, args);

		/// <summary>
		/// Runs the specified .NET Core tool with the specified arguments.
		/// </summary>
		/// <param name="name">The name of the .NET Core tool.</param>
		/// <param name="args">The arguments, if any.</param>
		public static void RunDotNetTool(string name, params string[] args)
		{
			if (s_toolsDirectory == null)
				throw new InvalidOperationException("Call SetDotNetToolsDirectory before RunDotNetTool.");
			if (!File.Exists(Path.Combine(s_toolsDirectory, $"{name}.exe")))
				RunDotNet("tool", "install", name, "--tool-path", s_toolsDirectory);
			RunApp(Path.Combine(s_toolsDirectory, name), args);
		}

		private static string s_toolsDirectory;
	}
}
