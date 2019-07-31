using System;
using System.Collections.Generic;

namespace Faithlife.Build
{
	/// <summary>
	/// Settings for cleaning projects.
	/// </summary>
	public sealed class DotNetCleanSettings
	{
		/// <summary>
		/// Called to find the directories to delete.
		/// </summary>
		public Func<IReadOnlyList<string>> FindDirectoriesToDelete { get; set; }

		/// <summary>
		/// Clones the settings.
		/// </summary>
		public DotNetCleanSettings Clone() => (DotNetCleanSettings) MemberwiseClone();
	}
}
