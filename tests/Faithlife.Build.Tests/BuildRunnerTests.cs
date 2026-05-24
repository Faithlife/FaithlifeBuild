using NUnit.Framework;

namespace Faithlife.Build.Tests;

internal sealed class BuildRunnerTests
{
	[Test]
	public void NullArgsThrows()
	{
		Assert.Throws<ArgumentNullException>(() => BuildRunner.Execute(null!, _ => { }));
	}

	[Test]
	public void NullInitializeThrows()
	{
		Assert.Throws<ArgumentNullException>(() => BuildRunner.Execute([], null!));
	}

	[Test]
	public void FailsOnMissingTarget()
	{
		using var error = new StringWriter();
		Console.SetError(error);

		var targetName = "target";

		Assert.That(BuildRunner.Execute([targetName], build => { }), Is.EqualTo(2));
		Assert.That(error.ToString(), Does.Contain($"Target not found: {targetName}"));
	}

	[Test]
	public void PrintsDefaultTargets()
	{
		using var output = new StringWriter();
		Console.SetOut(output);

		Assert.That(BuildRunner.Execute([], build => { }), Is.Zero);

		// Check for some known default targets
		var outputString = output.ToString();
		Assert.That(outputString, Does.Contain("-n|--dry-run"));
		Assert.That(outputString, Does.Contain("-s|--skip-dependencies"));
		Assert.That(outputString, Does.Contain("-?|-h|--help"));
	}

	[Test]
	public void DotNetTargetsOmitCoverageByDefault()
	{
		using var output = new StringWriter();
		Console.SetOut(output);

		Assert.That(BuildRunner.Execute([], build =>
		{
			build.AddDotNetTargets(new DotNetBuildSettings { SolutionName = "Test.sln" });
		}), Is.Zero);
		Assert.That(output.ToString(), Does.Not.Contain("coverage"));
	}

	[Test]
	public void DotNetTargetsIncludeCoverageWhenConfigured()
	{
		using var output = new StringWriter();
		Console.SetOut(output);

		Assert.That(BuildRunner.Execute([], build =>
		{
			build.AddDotNetTargets(new DotNetBuildSettings
			{
				CoverageSettings = new DotNetCoverageSettings(),
				SolutionName = "Test.sln",
			});
		}), Is.Zero);
		Assert.That(output.ToString(), Does.Match("coverage\\s+Runs tests with coverage and generates coverage reports"));
	}

	[Test]
	public void DotNetTargetsIncludeCoverageWhenCoverageRunSettingsExists()
	{
		var currentDirectory = Environment.CurrentDirectory;
		var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
		Directory.CreateDirectory(tempDirectory);

		try
		{
			Environment.CurrentDirectory = tempDirectory;
			File.WriteAllText("coverage.runsettings", "<RunSettings />");

			using var output = new StringWriter();
			Console.SetOut(output);

			Assert.That(BuildRunner.Execute([], build =>
			{
				build.AddDotNetTargets(new DotNetBuildSettings { SolutionName = "Test.sln" });
			}), Is.Zero);
			Assert.That(output.ToString(), Does.Match("coverage\\s+Runs tests with coverage and generates coverage reports"));
		}
		finally
		{
			Environment.CurrentDirectory = currentDirectory;
			Directory.Delete(tempDirectory, recursive: true);
		}
	}

	[Test]
	public void DotNetBuildSettingsCloneClonesCoverageSettings()
	{
		var settings = new DotNetBuildSettings
		{
			CoverageSettings = new DotNetCoverageSettings { TargetFramework = "net10.0" },
		};

		var clone = settings.Clone();

		Assert.That(clone.CoverageSettings, Is.Not.Null);
		Assert.That(clone.CoverageSettings, Is.Not.SameAs(settings.CoverageSettings));
		Assert.That(clone.CoverageSettings!.TargetFramework, Is.EqualTo("net10.0"));
	}

	[Test]
	public void CoverageRunSettingsDefaultToCoverageRunSettingsWhenPresent()
	{
		var currentDirectory = Environment.CurrentDirectory;
		var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
		Directory.CreateDirectory(tempDirectory);

		try
		{
			Environment.CurrentDirectory = tempDirectory;
			Assert.That(new DotNetCoverageSettings().GetCoverageRunSettingsPath(), Is.Null);

			File.WriteAllText("coverage.runsettings", "<RunSettings />");
			Assert.That(new DotNetCoverageSettings().GetCoverageRunSettingsPath(), Is.EqualTo("coverage.runsettings"));
			Assert.That(new DotNetCoverageSettings { RunSettingsPath = "custom.runsettings" }.GetCoverageRunSettingsPath(), Is.EqualTo("custom.runsettings"));
		}
		finally
		{
			Environment.CurrentDirectory = currentDirectory;
			Directory.Delete(tempDirectory, recursive: true);
		}
	}

	[Test]
	public void PrintsCustomTargets()
	{
		using var output = new StringWriter();
		Console.SetOut(output);

		var targetName = "target";
		var targetDescription = "This is a basic target.";

		Assert.That(BuildRunner.Execute([], build =>
		{
			build.Target(targetName)
				.Describe(targetDescription);
		}), Is.Zero);
		Assert.That(output.ToString(), Does.Match($"{targetName}\\s+{targetDescription}"));
	}

	[Test]
	public void FailsOnFailedTarget()
	{
		using var output = new StringWriter();
		Console.SetOut(output);

		var targetName = "target";

		Assert.That(BuildRunner.Execute(["--no-color", targetName], build =>
		{
			build.Target(targetName)
				.Does(() =>
				{
					throw new InvalidOperationException();
				});
		}), Is.EqualTo(1));
		Assert.That(output.ToString(), Does.Contain($"{targetName}: FAILED!"));
	}

	[Test]
	public void DryRunSkipsTargetExecution()
	{
		using var output = new StringWriter();
		Console.SetOut(output);

		var targetName = "target";

		Assert.That(BuildRunner.Execute(["--no-color", "--dry-run", targetName], build =>
		{
			build.Target(targetName)
				.Does(() =>
				{
					throw new InvalidOperationException();
				});
		}), Is.Zero);
		Assert.That(output.ToString(), Does.Contain($"Succeeded ({targetName}) (dry run)"));
	}

	[Test]
	public void ExecutesDependencies()
	{
		using var output = new StringWriter();
		Console.SetOut(output);

		var firstTarget = "firstTarget";
		var secondTarget = "secondTarget";

		Assert.That(BuildRunner.Execute(["--no-color", secondTarget], build =>
		{
			build.Target(firstTarget);

			build.Target(secondTarget)
				.DependsOn(firstTarget);
		}), Is.Zero);

		var outputString = output.ToString();
		Assert.That(outputString, Does.Contain($"{firstTarget}: Succeeded"));
		Assert.That(outputString, Does.Contain($"{secondTarget}: Succeeded"));
	}

	[Test]
	public void SkipsDependencies()
	{
		using var output = new StringWriter();
		Console.SetOut(output);

		var firstTarget = "firstTarget";
		var secondTarget = "secondTarget";

		Assert.That(BuildRunner.Execute(["--no-color", "--skip-dependencies", secondTarget], build =>
		{
			build.Target(firstTarget);

			build.Target(secondTarget)
				.DependsOn(firstTarget);
		}), Is.Zero);

		var outputString = output.ToString();
		Assert.That(outputString, Does.Not.Contain($"{firstTarget}: Succeeded"));
		Assert.That(outputString, Does.Contain($"{secondTarget}: Succeeded"));
	}

	[Test]
	public void SkipsSpecificDependencies()
	{
		using var output = new StringWriter();
		Console.SetOut(output);

		var firstTarget = "firstTarget";
		var secondTarget = "secondTarget";
		var thirdTarget = "thirdTarget";

		Assert.That(BuildRunner.Execute(["--no-color", "--skip", secondTarget, thirdTarget], build =>
		{
			build.Target(firstTarget);

			build.Target(secondTarget);

			build.Target(thirdTarget)
				.DependsOn(firstTarget)
				.DependsOn(secondTarget);
		}), Is.Zero);

		var outputString = output.ToString();
		Assert.That(outputString, Does.Contain($"{firstTarget}: Succeeded"));
		Assert.That(outputString, Does.Not.Contain($"{secondTarget}: Succeeded"));
		Assert.That(outputString, Does.Contain($"{thirdTarget}: Succeeded"));
	}
}
