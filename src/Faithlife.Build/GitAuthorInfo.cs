namespace Faithlife.Build;

/// <summary>
/// Information about a git commit author.
/// </summary>
public sealed class GitAuthorInfo
{
	/// <summary>
	/// Creates an instance.
	/// </summary>
	public GitAuthorInfo(string name, string email)
	{
		Name = name ?? throw new ArgumentNullException(nameof(name));
		Email = email ?? throw new ArgumentNullException(nameof(email));
	}

	/// <summary>
	/// The author name.
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// The author email.
	/// </summary>
	public string Email { get; }
}
