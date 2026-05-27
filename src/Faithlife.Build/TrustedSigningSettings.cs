namespace Faithlife.Build;

/// <summary>
/// Settings for signing packages using Azure Artifact Signing (formerly Trusted Signing).
/// </summary>
/// <remarks>For more information, see <a href="https://learn.microsoft.com/en-us/azure/artifact-signing/overview">What is Azure Artifact Signing?</a></remarks>
public sealed class TrustedSigningSettings
{
	/// <summary>
	/// The Azure Artifact Signing Account endpoint. The value must be a URI that aligns to the region that your Azure Artifact Signing Account and Certificate Profile were created in.
	/// </summary>
	public Uri? EndpointUrl { get; set; }

	/// <summary>
	/// The Azure Artifact Signing Account name.
	/// </summary>
	public string? Account { get; set; }

	/// <summary>
	/// The Certificate Profile name.
	/// </summary>
	public string? CertificateProfile { get; set; }

	/// <summary>
	/// Gets or sets a callback that can provide the access token to use for Azure Artifact Signing.
	/// </summary>
	/// <remarks>This is only used when signing on Linux; signing on Windows requires a managed identity. It should be acquired via <c>az account get-access-token --resource "https://codesigning.azure.net/" --query "accessToken" --output tsv</c> or similar.</remarks>
	public Func<string>? GetAccessToken { get; set; }
}
