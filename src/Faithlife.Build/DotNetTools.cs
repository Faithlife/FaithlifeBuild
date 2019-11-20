using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using static Faithlife.Build.AppRunner;
using static Faithlife.Build.DotNetRunner;

namespace Faithlife.Build
{
	/// <summary>
	/// Manages .NET Core Tools (local and global) and classic NuGet packaged tools, installed within a local directory.
	/// </summary>
	public sealed class DotNetTools
	{
		/// <summary>
		/// Prepares to install tools within the specified directory.
		/// </summary>
		/// <param name="directory">The directory path.</param>
		public DotNetTools(string directory)
		{
			m_directory = Path.GetFullPath(directory);
			m_sources = new List<string>();
			m_nugetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages", "nuget.commandline", "5.1.0", "tools", "NuGet.exe");
		}

		/// <summary>
		/// Provides access to the specified local .NET Core Tool, installing it if necessary.
		/// </summary>
		/// <param name="package">The package name. To install a particular version,
		/// indicate it after the name, separated by a slash.</param>
		/// <param name="name">The tool name, if it differs from the package name.</param>
		/// <returns>The <see cref="DotNetLocalTool" /> used to run the tool.</returns>
		public DotNetLocalTool GetLocalTool(string package, string? name = null)
		{
			var version = ExtractPackageVersion(ref package);

			var args = new List<string>();
			var directory = Path.Combine(m_directory, package, version ?? "latest");

			if (!Directory.Exists(directory) || !File.Exists(Path.Combine(directory, ".config", "dotnet-tools.json")))
			{
				Directory.CreateDirectory(directory);
				RunDotNet(new AppRunnerSettings { Arguments = new[] { "new", "tool-manifest" }, WorkingDirectory = directory });

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
				foreach (var source in m_sources)
				{
					args.Add("--add-source");
					args.Add(source);
				}

				RunDotNet(new AppRunnerSettings { Arguments = args, WorkingDirectory = directory });
			}

			return new DotNetLocalTool(directory, name ?? package);
		}

		/// <summary>
		/// Gets the path to the specified global .NET Core Tool, installing it if necessary.
		/// </summary>
		/// <param name="package">The package name. To install a particular version,
		/// indicate it after the name, separated by a slash.</param>
		/// <param name="name">The tool name, if it differs from the package name.</param>
		/// <returns>The path to the installed tool.</returns>
		public string GetToolPath(string package, string? name = null)
		{
			var version = ExtractPackageVersion(ref package);

			var args = new List<string>();
			var directory = Path.Combine(m_directory, package, version ?? "latest");

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
		/// Gets the path to the specified classic NuGet package tool, installing it if necessary.
		/// </summary>
		/// <param name="package">The package name. To install a particular version,
		/// indicate it after the name, separated by a slash.</param>
		/// <param name="name">The tool name, if it differs from the package name.</param>
		/// <returns>The path to the installed tool.</returns>
		public string GetClassicToolPath(string package, string? name = null)
		{
			var version = ExtractPackageVersion(ref package);

			var args = new List<string>
			{
				"install",
				package,
				"-Prerelease",
				"-NonInteractive",
				"-OutputDirectory",
				m_directory,
			};

			if (version != null)
			{
				args.Add("-Version");
				args.Add(version);
			}

			foreach (var source in m_sources)
			{
				args.Add("-Source");
				args.Add(source);
			}

			RunDotNetFrameworkApp(m_nugetPath, args);

			if (version == null)
			{
				version = Directory.GetDirectories(m_directory, $"{package}.*")
					.Select(x => (Path.GetFileName(x) ?? throw new InvalidOperationException()).Substring(package.Length + 1))
					.OrderByDescending(x => x, new NuGetVersionComparer())
					.First();
			}

			return Path.Combine(m_directory, $"{package}.{version}", "tools", name ?? package);
		}

		/// <summary>
		/// Adds the specified NuGet package source.
		/// </summary>
		/// <param name="source">The path or URL of the NuGet package source.</param>
		/// <returns>The <c>DotNetTools</c> instance, for use by the "fluent" builder pattern.</returns>
		public DotNetTools AddSource(string source)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));

			m_sources.Add(Regex.IsMatch(source, @"^\w+:") ? source : Path.GetFullPath(source));
			return this;
		}

		private string? ExtractPackageVersion(ref string package)
		{
			string? version = null;
			var slashIndex = package.IndexOf('/');
			if (slashIndex != -1)
			{
				version = package.Substring(slashIndex + 1);
				package = package.Substring(0, slashIndex);
			}

			return version;
		}

		private class NuGetVersionComparer : IComparer<string>
		{
			public int Compare(string left, string right)
			{
				if (left == null)
					return right == null ? 0 : -1;
				if (right == null)
					return 1;

				var leftHyphenParts = left.Split(new[] { '-' }, 2);
				var rightHyphenParts = right.Split(new[] { '-' }, 2);

				var leftDotParts = leftHyphenParts[0].Split('.');
				var rightDotParts = rightHyphenParts[0].Split('.');

				for (var index = 0; index < Math.Min(leftDotParts.Length, rightDotParts.Length); index++)
				{
					if (leftDotParts[index] != rightDotParts[index])
						return int.Parse(leftDotParts[index]).CompareTo(int.Parse(rightDotParts[index]));
				}

				if (leftDotParts.Length != rightDotParts.Length)
					return leftDotParts.Length.CompareTo(rightDotParts.Length);

				var leftSuffix = leftHyphenParts.ElementAtOrDefault(1);
				var rightSuffix = rightHyphenParts.ElementAtOrDefault(1);
				if (leftSuffix == null)
					return rightSuffix == null ? 0 : 1;
				if (rightSuffix == null)
					return -1;
				return string.CompareOrdinal(leftSuffix, rightSuffix);
			}
		}

		private readonly string m_directory;
		private readonly List<string> m_sources;
		private readonly string m_nugetPath;
	}
}
