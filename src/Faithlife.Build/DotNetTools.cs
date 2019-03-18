using System;
using System.IO;
using static Faithlife.Build.DotNetRunner;

namespace Faithlife.Build
{
	/// <summary>
	/// Manages .NET Core Global Tools, installed within a local directory.
	/// </summary>
	public sealed class DotNetTools
	{
		/// <summary>
		/// Prepares to install .NET Core Global Tools within the specified directory.
		/// </summary>
		/// <param name="directory">The directory path.</param>
		public DotNetTools(string directory)
		{
			m_directory = directory;
		}

		/// <summary>
		/// Gets the path to the specified tool, installing it if necessary.
		/// </summary>
		/// <param name="package">The package name. To install a particular version,
		/// indicate it after the name, separated by a slash.</param>
		/// <param name="name">The tool name, if it differs from the package name.</param>
		/// <returns>The path to the installed tool executable.</returns>
		public string GetToolPath(string package, string name = null)
		{
			string version = null;
			int slashIndex = package.IndexOf('/');
			if (slashIndex != -1)
			{
				version = package.Substring(slashIndex + 1);
				package = package.Substring(0, slashIndex);
			}

			string directory = Path.Combine(m_directory, package, version ?? "latest");
			string executable = Path.Combine(directory, $"{name ?? package}.exe");
			if (!File.Exists(executable))
				RunDotNet("tool", "install", package, "--tool-path", directory, version != null ? "--version" : null, version);
			if (!File.Exists(executable))
				throw new InvalidOperationException($"Failed to install {package} (version {version ?? "latest"}); missing executable: {executable}");
			return executable;
		}

		private readonly string m_directory;
	}
}
