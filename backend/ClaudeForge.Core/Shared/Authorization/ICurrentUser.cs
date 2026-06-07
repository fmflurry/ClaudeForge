namespace ClaudeForge.Core.Shared.Authorization;

/// <summary>
/// Represents the current caller's identity as seen by the domain.
/// Null <see cref="UserId"/> indicates an anonymous (unauthenticated) caller.
/// </summary>
public interface ICurrentUser
{
    /// <summary>The caller's user identifier, or <c>null</c> when anonymous.</summary>
    Guid? UserId { get; }

    /// <summary>
    /// <c>true</c> when the caller has a valid, authenticated session;
    /// <c>false</c> for anonymous callers.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>The caller's email address, or <c>null</c> when anonymous.</summary>
    string? Email { get; }
}
