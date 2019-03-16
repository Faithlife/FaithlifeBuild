using System;
using System.IO;
using McMaster.Extensions.CommandLineUtils;
using static Faithlife.Build.AppRunner;

namespace Faithlife.Build
{
	public static class DotNetRunner
	{
		public static void SetDotNetToolsDirectory(string path) => s_toolsDirectory = path;

		public static void RunDotNet(params string[] args) => RunApp(DotNetExe.FullPath, args);

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
