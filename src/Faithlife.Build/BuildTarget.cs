using System;
using System.Collections.Generic;

namespace Faithlife.Build
{
	public sealed class BuildTarget
	{
		public string Name { get; }

		public string Description { get; private set; }

		public IReadOnlyList<string> Dependencies => m_dependencies;

		public BuildTarget Describe(string description)
		{
			Description = description;
			return this;
		}

		public BuildTarget DependsOn(params string[] targets)
		{
			m_dependencies.AddRange(targets);
			return this;
		}

		public BuildTarget Does(Action action)
		{
			m_action = action;
			return this;
		}

		public void Run() => m_action?.Invoke();

		internal BuildTarget(string name)
		{
			Name = name;
			Description = "";
			m_dependencies = new List<string>();
		}

		private readonly List<string> m_dependencies;
		private Action m_action;
	}
}
