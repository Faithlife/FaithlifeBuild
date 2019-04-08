using System.Collections.Generic;
using System.IO;
using System.Linq;
using GlobExpressions;

namespace Faithlife.Build
{
	/// <summary>
	/// Helper methods for build scripts.
	/// </summary>
	/// <remarks>
	/// <para>See <see href="https://github.com/kthompson/glob" /> for documentation on globs.</para>
	/// <para>Consider calling these methods directly via <c>using static Faithlife.Build.BuildUtility;</c></para>
	/// </remarks>
	public static class BuildUtility
	{
		/// <summary>
		/// Finds the files matching the specified globs, from the current working directory.
		/// </summary>
		/// <param name="globs">The globs to match.</param>
		/// <returns>The paths of the matching files.</returns>
		public static IReadOnlyList<string> FindFiles(params string[] globs) => FindFilesFrom(".", globs);

		/// <summary>
		/// Finds the directories matching the specified globs, from the current working directory.
		/// </summary>
		/// <param name="globs">The globs to match.</param>
		/// <returns>The paths of the matching directories.</returns>
		public static IReadOnlyList<string> FindDirectories(params string[] globs) => FindDirectoriesFrom(".", globs);

		/// <summary>
		/// Finds the files matching the specified globs.
		/// </summary>
		/// <param name="directory">The starting directory.</param>
		/// <param name="globs">The globs to match.</param>
		/// <returns>The paths of the matching files.</returns>
		public static IReadOnlyList<string> FindFilesFrom(string directory, params string[] globs) =>
			globs.SelectMany(glob => Glob.Files(directory, glob, GlobOptions.CaseInsensitive)).Distinct().Select(path => Path.Combine(directory, path)).ToList();

		/// <summary>
		/// Finds the directories matching the specified globs.
		/// </summary>
		/// <param name="directory">The starting directory.</param>
		/// <param name="globs">The globs to match.</param>
		/// <returns>The paths of the matching directories.</returns>
		public static IReadOnlyList<string> FindDirectoriesFrom(string directory, params string[] globs) =>
			globs.SelectMany(glob => Glob.Directories(directory, glob, GlobOptions.CaseInsensitive)).Distinct().Select(path => Path.Combine(directory, path)).ToList();

		/// <summary>
		/// Copies the files matching the specified globs from one directory to another, creating subdirectories as needed.
		/// </summary>
		/// <param name="fromDirectory">The source directory.</param>
		/// <param name="toDirectory">The target directory.</param>
		/// <param name="globs">The globs to match. Use <c>"**"</c> to copy all files and directories.</param>
		public static void CopyFiles(string fromDirectory, string toDirectory, params string[] globs)
		{
			foreach (var filePath in globs.SelectMany(glob => Glob.Files(fromDirectory, glob, GlobOptions.CaseInsensitive)).Distinct())
			{
				if (Path.GetDirectoryName(filePath) is string directoryName)
					Directory.CreateDirectory(Path.Combine(toDirectory, directoryName));

				File.Copy(Path.Combine(fromDirectory, filePath), Path.Combine(toDirectory, filePath));
			}
		}
	}
}
