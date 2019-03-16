using McMaster.Extensions.CommandLineUtils;

namespace Faithlife.Build
{
	public sealed class BuildFlag
	{
		public bool Value => m_option.HasValue();

		internal BuildFlag(CommandOption option)
		{
			m_option = option;
		}

		private readonly CommandOption m_option;
	}
}
