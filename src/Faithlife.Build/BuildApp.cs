using System.Collections.Generic;
using McMaster.Extensions.CommandLineUtils;

namespace Faithlife.Build
{
	public sealed class BuildApp
	{
		public IReadOnlyList<BuildTarget> Targets => m_targets;

		public BuildFlag AddFlag(string template, string description) =>
			new BuildFlag(m_app.Option(template, description, CommandOptionType.NoValue));

		public BuildOption AddOption(string template, string description, string defaultValue = null) =>
			new BuildOption(m_app.Option(template, description, CommandOptionType.SingleValue), defaultValue);

		public BuildTarget Target(string name)
		{
			var target = new BuildTarget(name);
			m_targets.Add(target);
			return target;
		}

		internal BuildApp(CommandLineApplication app)
		{
			m_app = app;
			m_targets = new List<BuildTarget>();
		}

		private readonly CommandLineApplication m_app;
		private readonly List<BuildTarget> m_targets;
	}
}
