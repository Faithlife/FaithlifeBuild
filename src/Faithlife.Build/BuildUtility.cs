using System.Collections.Generic;
using System.Linq;
using GlobExpressions;

namespace Faithlife.Build
{
	public static class BuildUtility
	{
		public static IReadOnlyList<string> FindDirectories(params string[] globs) => globs.SelectMany(glob => Glob.Directories(".", glob)).ToList();

		public static IReadOnlyList<string> FindFiles(params string[] globs) => globs.SelectMany(glob => Glob.Files(".", glob)).ToList();
	}
}
