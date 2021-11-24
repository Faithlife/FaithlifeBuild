using GlobExpressions;
using Polly;

namespace Faithlife.Build;

/// <summary>
/// Helper methods for build scripts.
/// </summary>
/// <remarks>
/// <para>See <see href="https://github.com/kthompson/glob" /> for documentation on globs.</para>
/// <para>Consider calling these methods directly via <c>using static Faithlife.Build.BuildUtility;</c>.</para>
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
	public static IReadOnlyList<string> FindFilesFrom(string directory, params string[] globs)
	{
		if (directory is null)
			throw new ArgumentNullException(nameof(directory));
		if (globs is null)
			throw new ArgumentNullException(nameof(globs));

		return globs.SelectMany(glob => Glob.Files(directory, glob, GlobOptions.CaseInsensitive)).Distinct().Select(path => Path.Combine(directory, path)).ToList();
	}

	/// <summary>
	/// Finds the directories matching the specified globs.
	/// </summary>
	/// <param name="directory">The starting directory.</param>
	/// <param name="globs">The globs to match.</param>
	/// <returns>The paths of the matching directories.</returns>
	public static IReadOnlyList<string> FindDirectoriesFrom(string directory, params string[] globs)
	{
		if (directory is null)
			throw new ArgumentNullException(nameof(directory));
		if (globs is null)
			throw new ArgumentNullException(nameof(globs));

		return globs.SelectMany(glob => Glob.Directories(directory, glob, GlobOptions.CaseInsensitive)).Distinct().Select(path => Path.Combine(directory, path)).ToList();
	}

	/// <summary>
	/// Copies the files matching the specified globs from one directory to another, creating subdirectories and overwriting existing files as needed.
	/// </summary>
	/// <param name="fromDirectory">The source directory.</param>
	/// <param name="toDirectory">The target directory.</param>
	/// <param name="globs">The globs to match. Use <c>"**"</c> to copy all files and directories.</param>
	public static void CopyFiles(string fromDirectory, string toDirectory, params string[] globs)
	{
		if (fromDirectory is null)
			throw new ArgumentNullException(nameof(fromDirectory));
		if (toDirectory is null)
			throw new ArgumentNullException(nameof(toDirectory));
		if (globs is null)
			throw new ArgumentNullException(nameof(globs));

		CopyFilesCore(fromDirectory, toDirectory, globs.SelectMany(glob => Glob.Files(fromDirectory, glob, GlobOptions.CaseInsensitive)).Distinct());
	}

	/// <summary>
	/// Copies all files except those matching the specified globs from one directory to another, creating subdirectories and overwriting existing files as needed.
	/// </summary>
	/// <param name="fromDirectory">The source directory.</param>
	/// <param name="toDirectory">The target directory.</param>
	/// <param name="globs">The globs to exclude from copying.</param>
	public static void CopyFilesExcept(string fromDirectory, string toDirectory, params string[] globs)
	{
		if (fromDirectory is null)
			throw new ArgumentNullException(nameof(fromDirectory));
		if (toDirectory is null)
			throw new ArgumentNullException(nameof(toDirectory));
		if (globs is null)
			throw new ArgumentNullException(nameof(globs));

		CopyFilesCore(fromDirectory, toDirectory, Glob.Files(fromDirectory, "**").Except(globs.SelectMany(glob => Glob.Files(fromDirectory, glob, GlobOptions.CaseInsensitive)).Distinct()));
	}

	/// <summary>
	/// Recursively deletes the specified directory.
	/// </summary>
	/// <param name="path">The directory to delete.</param>
	/// <remarks>Retries once on error.</remarks>
	public static void DeleteDirectory(string path)
	{
		if (path is null)
			throw new ArgumentNullException(nameof(path));

		Policy.Handle<IOException>()
			.WaitAndRetry(new[] { TimeSpan.FromMilliseconds(50) })
			.Execute(() =>
			{
				try
				{
					Directory.Delete(path, recursive: true);
				}
				catch (DirectoryNotFoundException)
				{
				}
			});
	}

	private static void CopyFilesCore(string fromDirectory, string toDirectory, IEnumerable<string> filePaths)
	{
		foreach (var filePath in filePaths)
		{
			if (Path.GetDirectoryName(filePath) is string directoryName)
				Directory.CreateDirectory(Path.Combine(toDirectory, directoryName));

			File.Copy(Path.Combine(fromDirectory, filePath), Path.Combine(toDirectory, filePath), overwrite: true);
		}
	}

	internal static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> items)
		where T : class => items.Where(x => x is not null)!;
}
