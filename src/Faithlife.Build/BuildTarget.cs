namespace Faithlife.Build
{
	/// <summary>
	/// A build target.
	/// </summary>
	public sealed class BuildTarget
	{
		/// <summary>
		/// The target name.
		/// </summary>
		public string Name { get; }

		/// <summary>
		/// The target description, if any.
		/// </summary>
		public string Description { get; private set; }

		/// <summary>
		/// The names of targets upon which this target depends, if any.
		/// </summary>
		public IReadOnlyList<string> Dependencies => m_dependencies;

		/// <summary>
		/// Sets the target <see cref="Description"/>.
		/// </summary>
		/// <param name="description">The description.</param>
		/// <returns>The target, for use by the "fluent" builder pattern.</returns>
		public BuildTarget Describe(string description)
		{
			Description = description ?? throw new ArgumentNullException(nameof(description));
			return this;
		}

		/// <summary>
		/// Adds a target dependency by name.
		/// </summary>
		/// <param name="targets">The names of targets upon which this target depends.</param>
		/// <returns>The target, for use by the "fluent" builder pattern.</returns>
		public BuildTarget DependsOn(params string[] targets)
		{
			m_dependencies.AddRange(targets ?? throw new ArgumentNullException(nameof(targets)));
			return this;
		}

		/// <summary>
		/// Adds an action to the target.
		/// </summary>
		/// <param name="action">The target action.</param>
		/// <returns>The target, for use by the "fluent" builder pattern.</returns>
		public BuildTarget Does(Action action)
		{
			if (action is null)
				throw new ArgumentNullException(nameof(action));

			m_actions.Add(ActionAsync);
			return this;

			async Task ActionAsync() => action();
		}

		/// <summary>
		/// Adds an action to the target.
		/// </summary>
		/// <param name="action">The target action.</param>
		/// <returns>The target, for use by the "fluent" builder pattern.</returns>
		public BuildTarget Does(Func<Task> action)
		{
			if (action is null)
				throw new ArgumentNullException(nameof(action));

			m_actions.Add(action);
			return this;
		}

		/// <summary>
		/// Clears the actions on the target.
		/// </summary>
		/// <returns>The target, for use by the "fluent" builder pattern.</returns>
		public BuildTarget ClearActions()
		{
			m_actions.Clear();
			return this;
		}

		/// <summary>
		/// Runs the target action, if any.
		/// </summary>
		public void Run() => RunAsync().GetAwaiter().GetResult();

		/// <summary>
		/// Runs the target action, if any.
		/// </summary>
		public async Task RunAsync()
		{
			foreach (var action in m_actions)
				await action().ConfigureAwait(false);
		}

		internal BuildTarget(string name)
		{
			Name = name;
			Description = "";
			m_dependencies = new List<string>();
			m_actions = new List<Func<Task>>();
		}

		private readonly List<string> m_dependencies;
		private readonly List<Func<Task>> m_actions;
	}
}
