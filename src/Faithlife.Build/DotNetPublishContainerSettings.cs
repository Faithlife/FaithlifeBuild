namespace Faithlife.Build;

/// <summary>
/// Settings for publishing a .NET project to a Linux container.
/// </summary>
public sealed class DotNetPublishContainerSettings
{
	/// <summary>
	/// The container image family, or <c>null</c> to use the SDK default value.
	/// </summary>
	/// <remarks>See <a href="https://learn.microsoft.com/en-us/dotnet/core/containers/publish-configuration#containerfamily">ContainerFamily</a>.</remarks>
	public string? Family { get; set; }

	/// <summary>
	/// The tags to apply to the published container image.
	/// </summary>
	/// <remarks>See <a href="https://learn.microsoft.com/en-us/dotnet/core/containers/publish-configuration#containerimagetag">ContainerImageTags</a>.</remarks>
	public IReadOnlyList<string>? ImageTags { get; set; }

	/// <summary>
	/// The destination registry to publish to, or <c>null</c> to publish to the local Docker daemon.
	/// </summary>
	/// <remarks>See <a href="https://learn.microsoft.com/en-us/dotnet/core/containers/publish-configuration#containerregistry">ContainerRegistry</a>.</remarks>
	public string? Registry { get; set; }

	/// <summary>
	/// The container repository for the published image, or <c>null</c> to use the SDK default value.
	/// </summary>
	/// <remarks>See <a href="https://learn.microsoft.com/en-us/dotnet/core/containers/publish-configuration#containerrepository">ContainerRepository</a>.</remarks>
	public string? Repository { get; set; }
}
