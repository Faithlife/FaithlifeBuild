namespace Faithlife.Build;

internal static class PackageSigningHelper
{
	/// <summary>
	/// Returns an <c>Action{string}</c> that, given a package path, signs the package in place.
	/// </summary>
	public static Action<string> CreatePackageSigner(DotNetSigningSettings signingSettings)
	{
		ArgumentNullException.ThrowIfNull(signingSettings);

		return (BuildEnvironment.IsWindows(), BuildEnvironment.IsLinux(), signingSettings) switch
		{
			(_, _, { AzureKeyVaultSettings: null, TrustedSigningSettings: null }) => throw new BuildException("Either TrustedSigningSettings or AzureKeyVaultSettings must be specified."),
			(_, _, { AzureKeyVaultSettings: { }, TrustedSigningSettings: { } }) => throw new BuildException("Only one of TrustedSigningSettings or AzureKeyVaultSettings can be specified."),
			(false, false, _) => throw new BuildException("SigningSettings is only supported on Windows and Linux."),
			(true, _, { AzureKeyVaultSettings: { } azureSettings, TrustedSigningSettings: null }) => CreateWindowsAzureKeyVaultSigner(azureSettings),
			(false, _, { AzureKeyVaultSettings: { }, TrustedSigningSettings: null }) => throw new BuildException("SigningSettings.AzureKeyVaultSettings is only supported on Windows."),
			(true, _, { AzureKeyVaultSettings: null, TrustedSigningSettings: { } trustedSettings }) => CreateWindowsTrustedSigningSigner(trustedSettings),
			(false, true, { AzureKeyVaultSettings: null, TrustedSigningSettings: { } trustedSettings }) => CreateLinuxTrustedSigningSigner(trustedSettings),
			_ => throw new NotImplementedException(),
		};
	}

	private static Action<string> CreateWindowsAzureKeyVaultSigner(AzureKeyVaultSigningSettings azureSettings) =>
		CreateWindowsSigner([
			"sign", "code", "azure-key-vault",
			"-kvu", azureSettings.KeyVaultUrl?.AbsoluteUri ?? throw new BuildException("SigningSettings.AzureKeyVaultSettings.KeyVaultUrl is required."),
			"-kvc", azureSettings.CertificateName ?? throw new BuildException("SigningSettings.AzureKeyVaultSettings.CertificateName is required."),
		]);

	private static Action<string> CreateWindowsTrustedSigningSigner(TrustedSigningSettings trustedSettings)
	{
		if (!string.IsNullOrWhiteSpace(trustedSettings.AccessToken))
			Console.Error.WriteLine("Warning: SigningSettings.TrustedSigningSettings.AccessToken is ignored on Windows because dotnet sign uses managed identity.");

		return CreateWindowsSigner([
			"sign", "code", "trusted-signing",
			"-act", "azure-cli",
			"-tse", trustedSettings.EndpointUrl?.AbsoluteUri ?? throw new BuildException("SigningSettings.TrustedSigningSettings.EndpointUrl is required."),
			"-tsa", trustedSettings.Account ?? throw new BuildException("SigningSettings.TrustedSigningSettings.Account is required."),
			"-tscp", trustedSettings.CertificateProfile ?? throw new BuildException("SigningSettings.TrustedSigningSettings.CertificateProfile is required."),
			"-v", "information",
		]);
	}

	private static Action<string> CreateWindowsSigner(string[] signingArguments)
	{
		var toolPath = Path.Combine("release", "sign");
		DotNetRunner.RunDotNet(["tool", "install", "--tool-path", toolPath, "--prerelease", "sign"]);
		return packagePath => AppRunner.RunApp(Path.Combine(toolPath, "sign"), new AppRunnerSettings
		{
			Arguments = [.. signingArguments, packagePath],
		});
	}

	private static Action<string> CreateLinuxTrustedSigningSigner(TrustedSigningSettings trustedSettings)
	{
		var endpointUrl = trustedSettings.EndpointUrl?.AbsoluteUri ?? throw new BuildException("SigningSettings.TrustedSigningSettings.EndpointUrl is required.");
		var account = trustedSettings.Account ?? throw new BuildException("SigningSettings.TrustedSigningSettings.Account is required.");
		var certificateProfile = trustedSettings.CertificateProfile ?? throw new BuildException("SigningSettings.TrustedSigningSettings.CertificateProfile is required.");
		var accessToken = !string.IsNullOrWhiteSpace(trustedSettings.AccessToken) ? trustedSettings.AccessToken :
			throw new BuildException("SigningSettings.TrustedSigningSettings.AccessToken is required on Linux.");

		var toolPath = Path.Combine("release", "sign");
		DotNetRunner.RunDotNet(["tool", "install", "--tool-path", toolPath, "Devolutions.Psign.Tool"]);
		return packagePath =>
		{
			// input path needs to be relative
			var packagePathForSigning = Path.IsPathRooted(packagePath) ? Path.GetRelativePath(Environment.CurrentDirectory, packagePath) : packagePath;

			// generate a new path for the signed package because "`psign-tool code` signing execution currently requires --output to avoid in-place package mutation"; https://github.com/Devolutions/psign/blob/58c55e507394c4c1f94e8b8facff8d4dfa5021f7/src/code.rs#L208-L212
			// use the same directory so that we don't have to worry about cross-device file moves when replacing the original package with the signed package
			var packageFullPath = Path.GetFullPath(packagePath);
			var packageDirectory = Path.GetDirectoryName(packageFullPath) ?? Environment.CurrentDirectory;
			var signedPackagePath = Path.Combine(packageDirectory, $"{Path.GetFileNameWithoutExtension(packagePath)}.signed.nupkg");

			AppRunner.RunApp(Path.Combine(toolPath, "psign-tool"),
				new AppRunnerSettings
				{
					Arguments =
					[
						"code",
						"--mode", "portable",
						"--verbose",
						"--artifact-signing-endpoint", endpointUrl,
						"--artifact-signing-account-name", account,
						"--artifact-signing-profile-name", certificateProfile,
						"--artifact-signing-access-token", accessToken,
						"--timestamp-url", c_timestampUrl,
						"--timestamp-digest", "sha256",
						"--output", signedPackagePath,
						packagePathForSigning,
					],
					NoEcho = true,
				});

			// replace the input file with the signed package
			File.Move(signedPackagePath, packagePath, overwrite: true);
		};
	}

	private const string c_timestampUrl = "http://timestamp.acs.microsoft.com/";
}
