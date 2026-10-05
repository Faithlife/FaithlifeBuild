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
	/// The user name used to authenticate to <see cref="Registry"/>, or <c>null</c> to use the standard Docker authentication mechanism.
	/// </summary>
	/// <remarks>See <a href="https://github.com/dotnet/sdk-container-builds/blob/main/docs/RegistryAuthentication.md#authentication-via-environment-variables">Authentication via environment variables</a>.</remarks>
	public string? RegistryUserName { get; set; }

	/// <summary>
	/// The password (or token) used to authenticate to <see cref="Registry"/>, or <c>null</c> to use the standard Docker authentication mechanism.
	/// </summary>
	/// <remarks>See <a href="https://github.com/dotnet/sdk-container-builds/blob/main/docs/RegistryAuthentication.md#authentication-via-environment-variables">Authentication via environment variables</a>.</remarks>
	public string? RegistryPassword { get; set; }

	/// <summary>
	/// The container repository for the published image, or <c>null</c> to use the SDK default value.
	/// </summary>
	/// <remarks>See <a href="https://learn.microsoft.com/en-us/dotnet/core/containers/publish-configuration#containerrepository">ContainerRepository</a>.</remarks>
	public string? Repository { get; set; }
}
