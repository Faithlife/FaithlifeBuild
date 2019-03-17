namespace Faithlife.Build
{
	/// <summary>
	/// The properties of the build bot used to push to git as needed.
	/// </summary>
	public sealed class BuildBotSettings
	{
		/// <summary>
		/// The user name used by the build bot to log in to the git source.
		/// </summary>
		public string UserName { get; set; }

		/// <summary>
		/// The password used by the build bot to log in to the git source.
		/// </summary>
		public string Password { get; set; }

		/// <summary>
		/// The display name used by any commits.
		/// </summary>
		public string DisplayName { get; set; }

		/// <summary>
		/// The email address used by any commits.
		/// </summary>
		public string Email { get; set; }
	}
}
