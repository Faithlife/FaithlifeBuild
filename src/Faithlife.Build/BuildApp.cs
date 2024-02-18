using McMaster.Extensions.CommandLineUtils;

namespace Faithlife.Build;

/// <summary>
/// Represents the command-line application for the build.
/// </summary>
public sealed class BuildApp
{
	/// <summary>
	/// The targets previously added to the build via <see cref="Target"/>.
	/// </summary>
	public IReadOnlyList<BuildTarget> Targets => m_targets;

	/// <summary>
	/// The flags previously added to the build via <see cref="AddFlag"/>.
	/// </summary>
	public IReadOnlyList<BuildFlag> Flags => m_flags;

	/// <summary>
	/// The options previously added to the build via <see cref="AddOption"/>.
	/// </summary>
	public IReadOnlyList<BuildOption> Options => m_options;

	/// <summary>
	/// Adds support for a no-value command-line flag.
	/// </summary>
	/// <param name="template">The flag template, e.g. <c>"-q|--quiet"</c>.</param>
	/// <param name="description">The flag help description, e.g. <c>"Suppresses console output"</c>.</param>
	/// <returns>The added <see cref="BuildFlag"/>, which can be used from within a running target
	/// to determine whether the flag was set.</returns>
	public BuildFlag AddFlag(string template, string description)
	{
		var flag = new BuildFlag(template, m_app.Option(template, description, CommandOptionType.NoValue));
		m_flags.Add(flag);
		return flag;
	}

	/// <summary>
	/// Adds support for a single-value command-line option.
	/// </summary>
	/// <param name="template">The option template, e.g. <c>"-n|--name &lt;name&gt;"</c>.</param>
	/// <param name="description">The option help description, e.g. <c>"Sets the name"</c>.</param>
	/// <param name="defaultValue">The default value for the option; <c>null</c> if omitted.</param>
	/// <returns>The added <see cref="BuildOption"/>, which can be used from within a running target
	/// to determine whether the option was set, and to what value.</returns>
	public BuildOption AddOption(string template, string description, string? defaultValue = null)
	{
		var option = new BuildOption(template, m_app.Option(template, description, CommandOptionType.SingleValue), defaultValue);
		m_options.Add(option);
		return option;
	}

	/// <summary>
	/// Creates a build target.
	/// </summary>
	/// <param name="name">The name of the build target, e.g. <c>clean</c>.</param>
	/// <returns>The specified build target. If a target with the specified name already exists, it is returned.</returns>
	public BuildTarget Target(string name)
	{
		ArgumentNullException.ThrowIfNull(name);

		var target = m_targets.SingleOrDefault(x => x.Name == name);
		if (target is null)
			m_targets.Add(target = new BuildTarget(name));
		return target;
	}

	internal BuildApp(CommandLineApplication app)
	{
		m_app = app;
		m_targets = new List<BuildTarget>();
		m_flags = new List<BuildFlag>();
		m_options = new List<BuildOption>();
	}

	private readonly CommandLineApplication m_app;
	private readonly List<BuildTarget> m_targets;
	private readonly List<BuildFlag> m_flags;
	private readonly List<BuildOption> m_options;
}
