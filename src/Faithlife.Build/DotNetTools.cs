using System.Globalization;
using System.Text.RegularExpressions;
using NuGet.Versioning;
using static Faithlife.Build.AppRunner;
using static Faithlife.Build.DotNetRunner;

namespace Faithlife.Build;

/// <summary>
/// Manages .NET tools and classic NuGet packaged tools, installed within a local directory.
/// </summary>
[Obsolete("Use DotNetLocalTool and/or DotNetClassicTool.")]
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
		m_nugetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages", "nuget.commandline", "5.4.0", "tools", "NuGet.exe");
	}

	/// <summary>
	/// Provides access to the specified .NET local tool, installing it if necessary.
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
			RunDotNet(new AppRunnerSettings { Arguments = ["new", "tool-manifest"], WorkingDirectory = directory });

			args.Add("tool");
			args.Add("install");
			args.Add(package);

			if (version is not null)
			{
				args.Add("--version");
				args.Add(version);
			}
		}
		else if (version is null)
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

		return new DotNetLocalTool(directory, name ?? package, new NuGetVersion(0, 0, 0));
	}

	/// <summary>
	/// Gets the path to the specified .NET global tool, installing it if necessary.
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

			if (version is not null)
			{
				args.Add("--version");
				args.Add(version);
			}
		}
		else if (version is null)
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

		if (version is not null)
		{
			args.Add("-Version");
			args.Add(version);
		}

		foreach (var source in m_sources)
		{
			args.Add("-Source");
			args.Add(source);
		}

		RunApp(m_nugetPath, new AppRunnerSettings { Arguments = args, IsFrameworkApp = true });

		version ??= Directory.GetDirectories(m_directory, $"{package}.*")
			.Select(x => Path.GetFileName(x).Substring(package.Length + 1))
			.OrderByDescending(x => x, new NuGetVersionComparer())
			.First();

		return Path.Combine(m_directory, $"{package}.{version}", "tools", name ?? package);
	}

	/// <summary>
	/// Adds the specified NuGet package source.
	/// </summary>
	/// <param name="source">The path or URL of the NuGet package source.</param>
	/// <returns>The <c>DotNetTools</c> instance, for use by the "fluent" builder pattern.</returns>
	public DotNetTools AddSource(string source)
	{
		ArgumentNullException.ThrowIfNull(source);

		m_sources.Add(Regex.IsMatch(source, @"^\w+:") ? source : Path.GetFullPath(source));
		return this;
	}

	private static string? ExtractPackageVersion(ref string package)
	{
		string? version = null;
		if (package.IndexOf('/', StringComparison.Ordinal) is int slashIndex and not -1)
		{
			version = package[(slashIndex + 1)..];
			package = package[..slashIndex];
		}

		return version;
	}

	private sealed class NuGetVersionComparer : IComparer<string>
	{
		public int Compare(string? left, string? right)
		{
			if (left is null)
				return right is null ? 0 : -1;
			if (right is null)
				return 1;

			var leftHyphenParts = left.Split('-', 2);
			var rightHyphenParts = right.Split('-', 2);

			var leftDotParts = leftHyphenParts[0].Split('.');
			var rightDotParts = rightHyphenParts[0].Split('.');

			for (var index = 0; index < Math.Min(leftDotParts.Length, rightDotParts.Length); index++)
			{
				if (leftDotParts[index] != rightDotParts[index])
					return int.Parse(leftDotParts[index], CultureInfo.InvariantCulture).CompareTo(int.Parse(rightDotParts[index], CultureInfo.InvariantCulture));
			}

			if (leftDotParts.Length != rightDotParts.Length)
				return leftDotParts.Length.CompareTo(rightDotParts.Length);

			return (leftHyphenParts.ElementAtOrDefault(1), rightHyphenParts.ElementAtOrDefault(1)) switch
			{
				(null, null) => 0,
				(null, _) => 1,
				(_, null) => -1,
				(string leftSuffix, string rightSuffix) => string.CompareOrdinal(leftSuffix, rightSuffix),
			};
		}
	}

	private readonly string m_directory;
	private readonly List<string> m_sources;
	private readonly string m_nugetPath;
}
