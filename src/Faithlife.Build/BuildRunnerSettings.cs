namespace Faithlife.Build;

/// <summary>
/// Settings for the BuildRunner; see <see cref="BuildRunner"/>.
/// </summary>
public sealed class BuildRunnerSettings
{
	/// <summary>
	/// Allows the consumer's entry point to return void.
	/// </summary>
	/// <remarks>For most consumers a void return entry point would be a mistake, as generally we should return
	/// the result of BuildRunner.Execute. However, there are instances, such as unit testing, where we should allow
	/// for a void return entry point.</remarks>
	public bool AllowVoidEntrypoint { get; set; }
}
