using System.Security.Claims;
using ClaudeForge.Core.Shared.Exceptions;

namespace ClaudeForge.Core.Identity.Ports;

// ═══════════════════════════════════════════════════════════════════════════════
// Group 4 — OIDC Integration ports
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Verified identity claims extracted from a validated OIDC id_token.
/// </summary>
public sealed record VerifiedIdentity(
    string Subject,
    string Email,
    bool EmailVerified,
    string Name,
    /// <summary>
    /// H6 — The "nonce" claim from the id_token. Empty string when the IdP did not echo
    /// the nonce (e.g. mock adapters in tests that do not set it).
    /// Must be verified against <see cref="AuthFlowState.Nonce"/> in CompleteSignInUseCase.
    /// </summary>
    string Nonce = "");

/// <summary>
/// Result of provisioning or linking a user via an OIDC provider.
/// </summary>
public sealed record ProvisionedUser(
    Guid UserId,
    string Email,
    string DisplayName,
    bool IsNewUser);

/// <summary>
/// Thrown when a requested provider is not supported or not enabled.
/// Maps to HTTP 400 Bad Request via the global exception handler.
/// </summary>
public sealed class UnsupportedProviderException : ProblemDetailsException
{
    public string ProviderName { get; }

    public UnsupportedProviderException(string providerName)
        : base($"Provider '{providerName}' is not supported or not enabled.")
    {
        ProviderName = providerName;
    }
}

/// <summary>
/// Port for interacting with an external OIDC identity provider.
/// </summary>
public interface IIdentityProviderPort
{
    /// <summary>
    /// Builds the IdP authorization redirect URL.
    /// The <paramref name="nonce"/> is included so the IdP echoes it back in the id_token
    /// "nonce" claim (H6 — OIDC replay protection).
    /// </summary>
    string BuildAuthorizationUrl(
        string provider,
        string codeChallenge,
        string state,
        string redirectUri,
        string nonce = "");

    /// <summary>Exchanges an authorization code for the raw (signed JWT) id_token string.</summary>
    Task<string> ExchangeCodeAsync(
        string provider,
        string code,
        string codeVerifier,
        string redirectUri,
        CancellationToken ct = default);

    /// <summary>Validates the id_token and returns the verified identity claims.</summary>
    Task<VerifiedIdentity> ValidateIdTokenAsync(
        string provider,
        string rawIdToken,
        CancellationToken ct = default);
}

/// <summary>
/// Marker interface: an identity provider adapter that exposes its canonical provider name.
/// Used by <see cref="IIdentityProviderRegistry"/> to resolve adapters by name.
/// </summary>
public interface INamedIdentityProviderPort
{
    string ProviderName { get; }
}

/// <summary>
/// Resolves a registered, enabled OIDC provider adapter by name.
/// </summary>
public interface IIdentityProviderRegistry
{
    /// <summary>
    /// Returns the adapter for the named provider.
    /// Throws <see cref="UnsupportedProviderException"/> when the provider is unknown,
    /// not enabled, null, or empty.
    /// </summary>
    IIdentityProviderPort Resolve(string providerName);
}

/// <summary>
/// User profile data returned by <see cref="IUserStorePort.FindByIdAsync"/>.
/// Includes the user's org memberships with names.
/// </summary>
public sealed record UserProfile(
    Guid UserId,
    string Email,
    string DisplayName,
    IReadOnlyList<UserOrgMembership> OrgMemberships);

/// <summary>A single org membership entry for a user profile.</summary>
public sealed record UserOrgMembership(
    Guid OrgId,
    string OrgName,
    string Role);

/// <summary>
/// Port for provisioning new users or linking existing accounts via OIDC sign-in.
/// </summary>
public interface IUserStorePort
{
    /// <summary>
    /// Provisions a new user or links an existing account.
    /// Algorithm:
    ///   1. Existing (provider, subject) → update email + displayName, return IsNewUser=false.
    ///   2. Else if emailVerified and cross-provider linking enabled → find by email_normalized,
    ///      add user_identity row, return IsNewUser=false.
    ///   3. Else → create user + user_identity, return IsNewUser=true.
    /// </summary>
    Task<ProvisionedUser> ProvisionOrLinkAsync(
        string provider,
        string subject,
        string email,
        bool emailVerified,
        string displayName,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the user's profile including org memberships with org names.
    /// Returns <c>null</c> when the user does not exist.
    /// </summary>
    Task<UserProfile?> FindByIdAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>
/// Claims bundle used to issue an RS256 access token.
/// </summary>
public sealed record AccessTokenClaims(
    Guid UserId,
    string Email,
    string Name,
    string Provider);

/// <summary>
/// Core DTO representing a persisted refresh token row — returned by the store port
/// so use cases in Core never need to reference the Infrastructure RefreshTokenEntity.
/// </summary>
public sealed record RefreshTokenInfo(
    Guid Id,
    Guid UserId,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RevokedAt,
    Guid? RotatedTo,
    Guid RootId,
    string Provider);

/// <summary>
/// Command to create a new opaque refresh token for a user.
/// </summary>
public sealed record CreateRefreshTokenCommand(
    Guid UserId,
    int ExpiryDays,
    string Provider = "");

/// <summary>
/// Result of a successful refresh-token creation.
/// <c>PlainToken</c> is returned once and never persisted — caller must store it.
/// </summary>
public sealed record RefreshTokenResult(
    Guid Id,
    Guid UserId,
    string PlainToken,
    DateTimeOffset ExpiresAt);

/// <summary>
/// Result of a one-time-use token rotation.
/// </summary>
public sealed record RotateRefreshTokenResult(
    Guid NewId,
    string NewPlainToken,
    DateTimeOffset NewExpiresAt);

/// <summary>
/// A single RSA public key entry in a JWKS document.
/// </summary>
public sealed record JwksKey(
    string Kty,
    string Use,
    string Alg,
    string Kid,
    string N,
    string E);

/// <summary>
/// A JSON Web Key Set document carrying one or more public keys.
/// </summary>
public sealed record JwksDocument(IReadOnlyList<JwksKey> Keys);

/// <summary>
/// Port for issuing and validating RS256 JWT access tokens.
/// </summary>
public interface ITokenIssuerPort
{
    /// <summary>
    /// Issues an RS256-signed JWT. Jti is generated fresh each call (UUID).
    /// </summary>
    string IssueAccessToken(AccessTokenClaims claims);

    /// <summary>
    /// Validates a raw JWT string.
    /// Returns the <see cref="ClaimsPrincipal"/> on success.
    /// Throws <see cref="Microsoft.IdentityModel.Tokens.SecurityTokenException"/> (or a subtype)
    /// for any validation failure.
    /// </summary>
    ClaimsPrincipal ValidateAccessToken(string rawToken);
}

/// <summary>
/// Port for exposing the current RSA public keys as a JWKS document.
/// </summary>
public interface IJwksProvider
{
    /// <summary>
    /// Returns current (and during rotation: prior) public keys as a JWKS document.
    /// Always contains at least one key.
    /// </summary>
    JwksDocument GetCurrentKeys();
}

/// <summary>
/// Port for a denylist of revoked JWT identifiers (jti claim).
/// </summary>
public interface IRevokedJtiStorePort
{
    /// <summary>
    /// Adds a jti to the denylist. TTL = remaining token life.
    /// Idempotent: duplicate jti is silently ignored.
    /// </summary>
    Task AddAsync(string jti, DateTimeOffset tokenExpiresAt, CancellationToken ct = default);

    /// <summary>
    /// Returns true if the jti is in the denylist AND the entry has not expired.
    /// Empty/whitespace jti returns false without a DB hit.
    /// </summary>
    Task<bool> IsRevokedAsync(string jti, CancellationToken ct = default);
}
