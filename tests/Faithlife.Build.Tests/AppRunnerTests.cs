using NUnit.Framework;

namespace Faithlife.Build.Tests;

internal sealed class AppRunnerTests
{
	[Test]
	public void EnvironmentVariablesOverrideInheritedEnvironment()
	{
		var name = $"FAITHLIFE_BUILD_TEST_{Guid.NewGuid():N}";
		var previousValue = Environment.GetEnvironmentVariable(name);

		try
		{
			Environment.SetEnvironmentVariable(name, "inherited");

			var settings = new AppRunnerSettings
			{
				Arguments = OperatingSystem.IsWindows() ? ["/c", "exit", "0"] : [],
				NoEcho = true,
			};
			settings.EnvironmentVariables[name] = "supplied";

			Assert.That(AppRunner.RunApp(OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/true", settings), Is.Zero);
		}
		finally
		{
			Environment.SetEnvironmentVariable(name, previousValue);
		}
	}
}
