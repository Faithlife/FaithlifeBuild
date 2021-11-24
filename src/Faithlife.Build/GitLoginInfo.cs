namespace Faithlife.Build
{
	/// <summary>
	/// Information used to login to git.
	/// </summary>
	public sealed class GitLoginInfo
	{
		/// <summary>
		/// Creates an instance.
		/// </summary>
		public GitLoginInfo(string username, string password)
		{
			Username = username ?? throw new ArgumentNullException(nameof(username));
			Password = password ?? throw new ArgumentNullException(nameof(password));
		}

		/// <summary>
		/// The username.
		/// </summary>
		public string Username { get; }

		/// <summary>
		/// The password.
		/// </summary>
		public string Password { get; }
	}
}
