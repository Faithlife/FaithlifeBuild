using McMaster.Extensions.CommandLineUtils;

namespace Faithlife.Build
{
	/// <summary>
	/// A no-value command-line flag for the build.
	/// </summary>
	public sealed class BuildFlag
	{
		/// <summary>
		/// True if the flag was specified.
		/// </summary>
		/// <remarks>This properly must only be accessed while running a target.</remarks>
		public bool Value => m_option.HasValue();

		/// <summary>
		/// The flag template.
		/// </summary>
		public string Template { get; }

		internal BuildFlag(string template, CommandOption option)
		{
			Template = template;
			m_option = option;
		}

		private readonly CommandOption m_option;
	}
}
