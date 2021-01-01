using System;
using NUnit.Framework;

namespace Faithlife.Build.Tests
{
	[TestFixture]
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
			Assert.Throws<ArgumentNullException>(() => BuildRunner.Execute(Array.Empty<string>(), null!));
		}
	}
}
