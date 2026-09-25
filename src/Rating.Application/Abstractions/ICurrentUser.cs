namespace Rating.Application.Abstractions;

/// <summary>
/// Represents the authenticated caller for the current request. Implementations
/// resolve identity from the ambient <c>ClaimsPrincipal</c>. Values are absent
/// (<c>UserId</c> null, <c>IsAdmin</c> false) for anonymous requests.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Username { get; }
    bool IsAdmin { get; }
}
