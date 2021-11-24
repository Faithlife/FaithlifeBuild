namespace Faithlife.Build
{
	/// <summary>
	/// A build exception.
	/// </summary>
	/// <remarks>Use this exception to show only the exception message in build output
	/// without including the full call stack.</remarks>
	public class BuildException : Exception
	{
		/// <summary>
		/// Creates a build exception.
		/// </summary>
		public BuildException()
			: base("An unexpected build error occurred.")
		{
		}

		/// <summary>
		/// Creates a build exception.
		/// </summary>
		public BuildException(string message)
			: base(message)
		{
		}

		/// <summary>
		/// Creates a build exception.
		/// </summary>
		public BuildException(string message, Exception innerException)
			: base(message, innerException)
		{
		}
	}
}
