namespace ClaudeForge.Core.Identity.Ports;

/// <summary>
/// Server-side state for an in-progress PKCE/OAuth authorize flow.
/// Created during GET /auth/authorize and consumed (single-use) during POST /auth/token.
/// </summary>
public sealed record AuthFlowState(
    /// <summary>Opaque, random, URL-safe state parameter (min 32 chars).</summary>
    string State,

    /// <summary>PKCE code_verifier (43-128 URL-safe chars per RFC 7636).</summary>
    string CodeVerifier,

    /// <summary>Provider name that initiated the flow (e.g. "google").</summary>
    string Provider,

    /// <summary>Exact redirect_uri used when building the authorization URL.</summary>
    string RedirectUri,

    /// <summary>UTC expiry for this flow state (typically now + 5 min).</summary>
    DateTimeOffset ExpiresAt,

    /// <summary>
    /// H6 — Per-flow random nonce sent in the OIDC authorize request and echoed back
    /// in the id_token "nonce" claim. Verified in CompleteSignInUseCase to prevent
    /// id_token replay attacks (OIDC Core §3.1.2.1).
    /// </summary>
    string Nonce = "");

/// <summary>
/// Port for storing and consuming PKCE/state entries during the OAuth authorize flow.
/// Implementations must be safe for concurrent use (thread-safe).
/// </summary>
public interface IAuthFlowStatePort
{
    /// <summary>
    /// Stores a new auth flow state.
    /// Replaces any prior entry with the same <see cref="AuthFlowState.State"/> key.
    /// </summary>
    Task StoreAsync(AuthFlowState entry, CancellationToken ct = default);

    /// <summary>
    /// Returns and atomically deletes the state entry (single-use).
    /// Returns <c>null</c> if the state key is unknown, empty/whitespace, or has expired.
    /// </summary>
    Task<AuthFlowState?> ConsumeAsync(string state, CancellationToken ct = default);
}
