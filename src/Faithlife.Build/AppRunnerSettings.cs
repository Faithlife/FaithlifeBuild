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
		public IEnumerable<string> Arguments { get; set; }

		/// <summary>
		/// The working directory from which to run the app.
		/// </summary>
		public string WorkingDirectory { get; set; }

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
