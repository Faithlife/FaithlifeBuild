using System;
using System.Collections.Generic;

namespace Faithlife.Build
{
	/// <summary>
	/// Settings for running .NET tests.
	/// </summary>
	public sealed class DotNetTestSettings
	{
		/// <summary>
		/// Called to find the test assemblies.
		/// </summary>
		public Func<IReadOnlyList<string>> FindTestAssemblies { get; set; }

		/// <summary>
		/// Clones the settings.
		/// </summary>
		public DotNetTestSettings Clone() => (DotNetTestSettings) MemberwiseClone();
	}
}
