using System;
using System.Collections.Generic;
using System.Linq;
using static Faithlife.Build.DotNetRunner;

namespace Faithlife.Build
{
	/// <summary>
	/// Provides access to a .NET Core Tool, installed locally.
	/// </summary>
	public sealed class DotNetLocalTool
	{
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
				throw new ArgumentException("WorkingDirectory not supported for local tools.", nameof(settings));

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

		private readonly string m_directory;
		private readonly string m_name;
	}
}
