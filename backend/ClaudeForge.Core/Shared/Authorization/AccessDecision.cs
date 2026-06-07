namespace ClaudeForge.Core.Shared.Authorization;

/// <summary>
/// Represents the outcome of an authorization decision.
/// </summary>
public enum AccessDecision
{
    /// <summary>Caller is permitted. Maps to HTTP 200.</summary>
    Allow,

    /// <summary>
    /// Resource not visible to caller (private plugin, authenticated non-member).
    /// Maps to HTTP 404 for non-disclosure.
    /// </summary>
    NotFound,

    /// <summary>
    /// Caller is anonymous but authentication is required.
    /// Maps to HTTP 401.
    /// </summary>
    Unauthenticated,

    /// <summary>
    /// Authenticated caller attempting a disallowed write.
    /// Maps to HTTP 403.
    /// </summary>
    Forbidden,
}
