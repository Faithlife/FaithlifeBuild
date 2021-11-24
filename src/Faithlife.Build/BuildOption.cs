using McMaster.Extensions.CommandLineUtils;

namespace Faithlife.Build;

/// <summary>
/// A single-value command-line option for the build.
/// </summary>
public sealed class BuildOption
{
	/// <summary>
	/// The option value, or <c>null</c> if the option was not specified.
	/// </summary>
	/// <remarks>This properly must only be accessed while running a target.</remarks>
	public string? Value => m_option.HasValue() ? m_option.Value() : m_defaultValue;

	/// <summary>
	/// The option template.
	/// </summary>
	public string Template { get; }

	internal BuildOption(string template, CommandOption option, string? defaultValue)
	{
		Template = template;
		m_option = option;
		m_defaultValue = defaultValue;
	}

	private readonly CommandOption m_option;
	private readonly string? m_defaultValue;
}
