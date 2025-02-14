namespace Faithlife.Build;

/// <summary>
/// Settings for signing packages using Azure Trusted Signing.
/// </summary>
/// <remarks>For more information, see <a href="https://learn.microsoft.com/en-us/azure/trusted-signing/overview">What is Trusted Signing?</a></remarks>
public sealed class TrustedSigningSettings
{
	/// <summary>
	/// The Trusted Signing Account endpoint. The value must be a URI that aligns to the region that your Trusted Signing Account and Certificate Profile were created in.
	/// </summary>
	public Uri? EndpointUrl { get; set; }

	/// <summary>
	/// The Trusted Signing Account name.
	/// </summary>
	public string? Account { get; set; }

	/// <summary>
	/// The Certificate Profile name.
	/// </summary>
	public string? CertificateProfile { get; set; }
}
