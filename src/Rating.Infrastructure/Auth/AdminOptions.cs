namespace Rating.Infrastructure.Auth;

/// <summary>
/// Hardcoded admin credentials, bound from <c>Admin:</c> configuration.
/// In production these come from environment variables
/// (<c>Admin__Username</c>, <c>Admin__Password</c>). This is intentionally a
/// single account: full user management is out of scope until the OIDC swap.
/// </summary>
public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}
