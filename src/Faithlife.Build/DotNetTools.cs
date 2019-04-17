using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
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
			m_directory = Path.GetFullPath(directory);
			m_sources = new List<string>();
		}

		/// <summary>
		/// Gets the path to the specified tool, installing it if necessary.
		/// </summary>
		/// <param name="package">The package name. To install a particular version,
		/// indicate it after the name, separated by a slash.</param>
		/// <param name="name">The tool name, if it differs from the package name.</param>
		/// <returns>The path to the installed tool.</returns>
		public string GetToolPath(string package, string name = null)
		{
			string version = null;
			int slashIndex = package.IndexOf('/');
			if (slashIndex != -1)
			{
				version = package.Substring(slashIndex + 1);
				package = package.Substring(0, slashIndex);
			}

			var args = new List<string>();
			string directory = Path.Combine(m_directory, package, version ?? "latest");

			if (!Directory.Exists(directory))
			{
				args.Add("tool");
				args.Add("install");
				args.Add(package);

				if (version != null)
				{
					args.Add("--version");
					args.Add(version);
				}
			}
			else if (version == null)
			{
				args.Add("tool");
				args.Add("update");
				args.Add(package);
			}

			if (args.Count != 0)
			{
				args.Add("--tool-path");
				args.Add(directory);

				foreach (var source in m_sources)
				{
					args.Add("--add-source");
					args.Add(source);
				}

				RunDotNet(args);
			}

			return Path.Combine(directory, name ?? package);
		}

		/// <summary>
		/// Adds the specified NuGet package source.
		/// </summary>
		/// <param name="source">The path or URL of the NuGet package source.</param>
		/// <returns>The <c>DotNetTools</c> instance, for use by the "fluent" builder pattern.</returns>
		public DotNetTools AddSource(string source)
		{
			m_sources.Add(Regex.IsMatch(source, @"^\w+:") ? source : Path.GetFullPath(source));
			return this;
		}

		private readonly string m_directory;
		private readonly List<string> m_sources;
	}
}
