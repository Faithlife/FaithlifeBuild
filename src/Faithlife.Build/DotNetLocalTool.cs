using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using static Faithlife.Build.DotNetRunner;

namespace Faithlife.Build
{
	/// <summary>
	/// Provides access to a .NET local tool.
	/// </summary>
	public sealed class DotNetLocalTool
	{
		/// <summary>
		/// Accesses a .NET local tool at the specified directory.
		/// </summary>
		/// <param name="name">The package name or command name of the tool.</param>
		/// <exception cref="BuildException">The tool is not installed.</exception>
		public static DotNetLocalTool Create(string name) => CreateFrom(".", name);

		/// <summary>
		/// Accesses a .NET local tool at the current directory.
		/// </summary>
		/// <param name="name">The package name or command name of the tool.</param>
		/// <returns>Null if the tool is not installed.</returns>
		public static DotNetLocalTool? TryCreate(string name) => TryCreateFrom(".", name);

		/// <summary>
		/// Accesses a .NET local tool at the specified directory.
		/// </summary>
		/// <param name="directory">The directory from which the tool should be run.</param>
		/// <param name="name">The package name or command name of the tool.</param>
		/// <exception cref="BuildException">The tool is not installed.</exception>
		public static DotNetLocalTool CreateFrom(string directory, string name) =>
			TryCreateFrom(directory, name) ?? throw new BuildException($"Tool '{name}' is not installed.");

		/// <summary>
		/// Accesses a .NET local tool at the specified directory.
		/// </summary>
		/// <param name="directory">The directory from which the tool should be run.</param>
		/// <param name="name">The package name or command name of the tool.</param>
		/// <returns>Null if the tool is not installed.</returns>
		public static DotNetLocalTool? TryCreateFrom(string directory, string name)
		{
			var allTools = GetDotNetLocalTools(directory);
			var foundTools = allTools.Where(x => string.Equals(x.Package, name, StringComparison.OrdinalIgnoreCase)).ToList();
			if (foundTools.Count == 0)
				foundTools = allTools.Where(x => string.Equals(x.Command, name, StringComparison.OrdinalIgnoreCase)).ToList();
			if (foundTools.Count == 0)
				return null;
			if (foundTools.Count > 1)
				throw new BuildException($"Multiple tools were found matching '{name}'.");
			return new DotNetLocalTool(directory, foundTools[0].Command);
		}

		/// <summary>
		/// True if there are any .NET local tools at the current directory.
		/// </summary>
		public static bool Any() => AnyFrom(".");

		/// <summary>
		/// True if there are any .NET local tools at the specified directory.
		/// </summary>
		/// <param name="directory">The directory from which the tool would be run.</param>
		public static bool AnyFrom(string directory) => GetDotNetLocalTools(directory).Any();

		/// <summary>
		/// Runs the local tool with the specified arguments.
		/// </summary>
		/// <param name="args">The command-line arguments.</param>
		public void Run(params string?[] args)
		{
			if (args == null)
				throw new ArgumentNullException(nameof(args));

			Run(args.AsEnumerable());
		}

		/// <summary>
		/// Runs the local tool with the specified arguments.
		/// </summary>
		/// <param name="args">The command-line arguments.</param>
		public void Run(IEnumerable<string?> args) => Run(new AppRunnerSettings { Arguments = args });

		/// <summary>
		/// Runs the local tool with the specified settings.
		/// </summary>
		/// <param name="settings">The settings to use when running the tool.</param>
		public int Run(AppRunnerSettings settings)
		{
			if (settings == null)
				throw new ArgumentNullException(nameof(settings));

			if (settings.WorkingDirectory != null)
				throw new ArgumentException($"{nameof(settings.WorkingDirectory)} not supported for local tools.", nameof(settings));

			settings = settings.Clone();
			settings.Arguments = new[] { "tool", "run", m_name, "--" }.Concat(settings.Arguments ?? Enumerable.Empty<string>());
			settings.WorkingDirectory = m_directory;

			return RunDotNet(settings);
		}

		internal DotNetLocalTool(string directory, string name)
		{
			m_directory = directory;
			m_name = name;
		}

		private static IReadOnlyList<(string Package, string Command)> GetDotNetLocalTools(string directory)
		{
			var manifestPath = TryGetDotNetLocalToolManifestPath(Path.GetFullPath(directory));
			if (manifestPath is null)
				return Array.Empty<(string, string)>();

			return JsonDocument.Parse(File.ReadAllText(manifestPath))
				.RootElement
				.GetProperty("tools")
				.EnumerateObject()
				.SelectMany(tool => tool.Value.GetProperty("commands").EnumerateArray().Select(x => (tool.Name, x.GetString()!)))
				.ToList();
		}

		private static string? TryGetDotNetLocalToolManifestPath(string directory)
		{
			while (true)
			{
				var configPath = Path.Combine(directory, ".config", "dotnet-tools.json");
				if (File.Exists(configPath))
					return configPath;

				var rootPath = Path.Combine(directory, "dotnet-tools.json");
				if (File.Exists(rootPath))
					return rootPath;

				var parent = Path.GetDirectoryName(directory);
				if (parent is null)
					return null;
				directory = parent;
			}
		}

		private readonly string m_directory;
		private readonly string m_name;
	}
}
