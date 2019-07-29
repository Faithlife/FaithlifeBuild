using System;
using System.Collections.Generic;

namespace Faithlife.Build
{
	/// <summary>
	/// Settings for creating NuGet packages.
	/// </summary>
	public sealed class DotNetPackageSettings
	{
		/// <summary>
		/// Called to find the projects to package.
		/// </summary>
		public Func<IReadOnlyList<string>> FindProjects { get; set; }

		/// <summary>
		/// Clones the settings.
		/// </summary>
		public DotNetPackageSettings Clone() => (DotNetPackageSettings) MemberwiseClone();
	}
}
