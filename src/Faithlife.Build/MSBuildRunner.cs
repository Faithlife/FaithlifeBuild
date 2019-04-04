using System;
using System.IO;
using static Faithlife.Build.AppRunner;

namespace Faithlife.Build
{
	/// <summary>
	/// Runs MSBuild.
	/// </summary>
	/// <remarks>
	/// <para>Consider calling these methods directly via <c>using static Faithlife.Build.MSBuildRunner;</c></para>
	/// </remarks>
	public static class MSBuildRunner
	{
		/// <summary>
		/// Gets the path of MSBuild for the specified version.
		/// </summary>
		public static string GetMSBuildPath(MSBuildVersion version)
		{
			(string Year, string Version) getPathParts()
			{
				if (version == MSBuildVersion.VS2017)
					return ("2017", "15.0");
				else if (version == MSBuildVersion.VS2019)
					return ("2019", "16.0");
				else
					throw new ArgumentException("Invalid version.", nameof(version));
			}

			var parts = getPathParts();
			foreach (string edition in new[] { "Enterprise", "Professional", "Community", "BuildTools", "Preview" })
			{
				string msbuildPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
					"Microsoft Visual Studio", parts.Year, edition, "MSBuild", parts.Version, "Bin", "amd64", "MSBuild.exe");
				if (File.Exists(msbuildPath))
					return msbuildPath;
			}

			throw new InvalidOperationException("MSBuild not found.");
		}

		/// <summary>
		/// Runs MSBuild with the specified arguments.
		/// </summary>
		/// <param name="version">The version of MSBuild.</param>
		/// <param name="args">The arguments, if any.</param>
		public static void RunMSBuild(MSBuildVersion version, params string[] args) =>
			RunDotNetFrameworkApp(GetMSBuildPath(MSBuildVersion.VS2017), args);
	}
}
