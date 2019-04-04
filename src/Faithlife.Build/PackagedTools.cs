using System;
using System.IO;

namespace Faithlife.Build
{
	/// <summary>
	/// Manages tools installed via NuGet packages.
	/// </summary>
	/// <remarks>The referenced NuGet packages must already be downloaded.
	/// Use a <c>PackageReference</c> in your build project to ensure that the desired
	/// NuGet package is downloaded.</remarks>
	public sealed class PackagedTools
	{
		/// <summary>
		/// Finds tools in the default global NuGet packages directory.
		/// </summary>
		public PackagedTools()
			: this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages"))
		{
		}

		/// <summary>
		/// Finds NuGet tools in the specified NuGet packages directory.
		/// </summary>
		/// <param name="directory">The directory path.</param>
		public PackagedTools(string directory)
		{
			m_directory = directory;
		}

		/// <summary>
		/// Gets the path to the specified tool.
		/// </summary>
		/// <param name="package">The package name and version, separated by a slash.</param>
		/// <param name="name">The tool name, if it differs from the package name.</param>
		/// <returns>The path to the installed tool.</returns>
		public string GetToolPath(string package, string name = null)
		{
			int slashIndex = package.IndexOf('/');
			if (slashIndex == -1)
				throw new ArgumentException("The package version must be specified after a slash.");
			string version = package.Substring(slashIndex + 1);
			package = package.Substring(0, slashIndex);

			return Path.Combine(m_directory, package, version, "tools", name ?? package);
		}

		private readonly string m_directory;
	}
}
