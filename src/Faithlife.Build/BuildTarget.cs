using System;
using System.Collections.Generic;

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
			m_action += action ?? throw new ArgumentNullException(nameof(action));
			return this;
		}

		/// <summary>
		/// Runs the target action, if any.
		/// </summary>
		public void Run() => m_action?.Invoke();

		internal BuildTarget(string name)
		{
			Name = name;
			Description = "";
			m_dependencies = new List<string>();
		}

		private readonly List<string> m_dependencies;
		private Action? m_action;
	}
}
