using System;
using System.Collections.Generic;

namespace Faithlife.Build
{
	/// <summary>
	/// Settings for creating and publishing NuGet packages.
	/// </summary>
	public sealed class DotNetPackageSettings
	{
		/// <summary>
		/// Called to find the projects to package.
		/// </summary>
		public Func<IReadOnlyList<string>>? FindProjects { get; set; }

		/// <summary>
		/// Set if a git tag should be pushed when a NuGet package is published.
		/// </summary>
		/// <remarks>The delegate calculates the name of the tag from the package
		/// information, e.g. <c>x => $"nuget.{x.Version}"</c>.</remarks>
		public Func<DotNetPackageInfo, string?>? PushTagOnPublish { get; set; }

		/// <summary>
		/// Clones the settings.
		/// </summary>
		public DotNetPackageSettings Clone() => (DotNetPackageSettings) MemberwiseClone();
	}
}
