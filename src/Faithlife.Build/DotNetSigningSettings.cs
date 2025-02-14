namespace Faithlife.Build;

/// <summary>
/// Settings for signing NuGet packages.
/// </summary>
/// <remarks>Only one of <see cref="TrustedSigningSettings"/> or <see cref="AzureKeyVaultSettings"/> must to specify the certificate to use for signing.</remarks>
public sealed class DotNetSigningSettings
{
	/// <summary>
	/// Settings for signing packages using Azure Trusted Signing.
	/// </summary>
	public TrustedSigningSettings? TrustedSigningSettings { get; set; }

	/// <summary>
	/// Settings for signing packages using a certificate stored in Azure Key Vault.
	/// </summary>
	public AzureKeyVaultSigningSettings? AzureKeyVaultSettings { get; set; }
}
