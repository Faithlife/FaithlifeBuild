using System.Runtime.InteropServices;

namespace Faithlife.Build;

/// <summary>
/// Information about the build environment.
/// </summary>
public static class BuildEnvironment
{
	/// <summary>
	/// Returns true if the build platform is Windows.
	/// </summary>
	public static bool IsWindows() => IsPlatform(OSPlatform.Windows);

	/// <summary>
	/// Returns true if the build platform is macOS.
	/// </summary>
	public static bool IsMacOS() => IsPlatform(OSPlatform.OSX);

	/// <summary>
	/// Returns true if the build platform is Linux.
	/// </summary>
	public static bool IsLinux() => IsPlatform(OSPlatform.Linux);

	/// <summary>
	/// Returns true if the build platform is Mac or Linux.
	/// </summary>
	public static bool IsUnix() => IsMacOS() || IsLinux();

	/// <summary>
	/// Returns true if the build platform is 64-bit.
	/// </summary>
	public static bool Is64Bit() => Environment.Is64BitOperatingSystem;

	private static bool IsPlatform(OSPlatform platform)
	{
		try
		{
			return RuntimeInformation.IsOSPlatform(platform);
		}
		catch (PlatformNotSupportedException)
		{
			return false;
		}
	}
}
