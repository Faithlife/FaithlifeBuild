using NUnit.Framework;

namespace Faithlife.Build.Tests;

public class BuildRunnerTests
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
}
