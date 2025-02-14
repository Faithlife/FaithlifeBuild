namespace Faithlife.Build;

/// <summary>
/// Settings for signing packages using Azure Key Vault.
/// </summary>
public sealed class AzureKeyVaultSigningSettings
{
	/// <summary>
	/// The Azure Key Vault URL.
	/// </summary>
	public Uri? KeyVaultUrl { get; set; }

	/// <summary>
	/// The Azure Key Vault certificate name.
	/// </summary>
	public string? CertificateName { get; set; }
}
