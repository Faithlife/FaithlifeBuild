using System;
using System.Collections.Generic;
using System.Linq;

namespace Faithlife.Build
{
	/// <summary>
	/// Settings for running apps.
	/// </summary>
	public sealed class AppRunnerSettings
	{
		/// <summary>
		/// The arguments to pass to the app.
		/// </summary>
		public IEnumerable<string?>? Arguments { get; set; }

		/// <summary>
		/// The working directory from which to run the app.
		/// </summary>
		public string? WorkingDirectory { get; set; }

		/// <summary>
		/// True if the process information should not be written to standard error.
		/// </summary>
		public bool NoEcho { get; set; }

		/// <summary>
		/// Called to determine if the exit code is successful; it if isn't, an exception is thrown.
		/// </summary>
		public Func<int, bool>? IsExitCodeSuccess { get; set; }

		/// <summary>
		/// True if Mono is used to run the app on Linux and macOS.
		/// </summary>
		public bool IsFrameworkApp { get; set; }

		/// <summary>
		/// True to run the app via <c>cmd /c</c> on Windows.
		/// </summary>
		public bool UseCmdOnWindows { get; set; }

		/// <summary>
		/// Clones the settings.
		/// </summary>
		public AppRunnerSettings Clone()
		{
			var clone = (AppRunnerSettings) MemberwiseClone();
			clone.Arguments = clone.Arguments?.ToList();
			return clone;
		}
	}
}
