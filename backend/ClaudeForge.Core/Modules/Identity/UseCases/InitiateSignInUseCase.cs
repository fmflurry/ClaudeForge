using System.Security.Cryptography;
using System.Text;
using ClaudeForge.Core.Identity.Ports;
using ClaudeForge.Core.Shared.Exceptions;

namespace ClaudeForge.Core.Modules.Identity.UseCases;

/// <summary>
/// Initiates an OAuth/OIDC sign-in flow by building the IdP authorization URL
/// and storing the PKCE state server-side.
/// </summary>
public sealed class InitiateSignInUseCase
{
    private readonly IIdentityProviderRegistry _registry;
    private readonly IAuthFlowStatePort _stateStore;

    /// <summary>
    /// The server-configured redirect URI for this provider (from OIDC__{provider}__REDIRECTURI).
    /// Used as the default and as the canonical allowed value.
    /// </summary>
    private readonly string _configuredRedirectUri;

    /// <summary>
    /// Optional loopback redirect URI allowed for CLI device-code flows
    /// (from OIDC__ALLOWEDLOOPBACKREDIRECT). May be null/empty if not configured.
    /// </summary>
    private readonly string? _allowedLoopbackRedirect;

    /// <summary>Default TTL for stored auth flow state (5 minutes).</summary>
    private static readonly TimeSpan StateTtl = TimeSpan.FromMinutes(5);

    public InitiateSignInUseCase(
        IIdentityProviderRegistry registry,
        IAuthFlowStatePort stateStore,
        string configuredRedirectUri,
        string? allowedLoopbackRedirect = null)
    {
        _registry = registry;
        _stateStore = stateStore;
        _configuredRedirectUri = configuredRedirectUri;
        _allowedLoopbackRedirect = allowedLoopbackRedirect;
    }

    /// <summary>
    /// Resolves the provider, validates the redirect URI, generates PKCE + state + nonce,
    /// stores the flow state, and returns the authorization URL.
    /// Throws <see cref="UnsupportedProviderException"/> for unknown providers (→ HTTP 400).
    /// Throws <see cref="ProblemDetailsException"/> for unregistered redirect URIs (→ HTTP 400).
    /// </summary>
    public async Task<InitiateSignInResult> ExecuteAsync(
        string provider,
        string? customRedirectUri,
        CancellationToken ct = default)
    {
        // Resolve provider — throws UnsupportedProviderException for unknown providers.
        IIdentityProviderPort providerPort = _registry.Resolve(provider);

        // C4: Validate (or default) the redirect URI.
        // Never use a raw client-supplied redirect verbatim without checking it.
        string redirectUri = ValidateRedirectUri(customRedirectUri);

        // Generate PKCE verifier (RFC 7636: 43-128 URL-safe chars).
        string codeVerifier = GenerateCodeVerifier();

        // Generate S256 code challenge: BASE64URL(SHA256(ASCII(verifier))).
        string codeChallenge = GenerateCodeChallenge(codeVerifier);

        // Generate opaque state (min 32 URL-safe chars).
        string state = GenerateState();

        // H6: Generate a per-flow random nonce for OIDC replay protection.
        string nonce = GenerateNonce();

        // Build authorization URL via the provider adapter (includes nonce parameter).
        string authorizationUrl = providerPort.BuildAuthorizationUrl(
            provider, codeChallenge, state, redirectUri, nonce);

        // Store auth flow state with TTL (nonce is bound to this flow).
        AuthFlowState flowState = new(
            State: state,
            CodeVerifier: codeVerifier,
            Provider: provider,
            RedirectUri: redirectUri,
            ExpiresAt: DateTimeOffset.UtcNow.Add(StateTtl),
            Nonce: nonce);

        await _stateStore.StoreAsync(flowState, ct);

        return new InitiateSignInResult(authorizationUrl, state);
    }

    /// <summary>
    /// Validates the caller-supplied redirect URI against the server-configured allowed values.
    /// Returns the URI to use (falls back to the configured URI when none is supplied).
    /// Throws <see cref="ProblemDetailsException"/> (→ HTTP 400) when the supplied URI is
    /// not in the allowed set.
    /// </summary>
    private string ValidateRedirectUri(string? customRedirectUri)
    {
        if (string.IsNullOrWhiteSpace(customRedirectUri))
        {
            // Default to the server-configured redirect URI.
            return _configuredRedirectUri;
        }

        // Allow exact match against the configured provider redirect URI.
        if (string.Equals(customRedirectUri, _configuredRedirectUri, StringComparison.Ordinal))
        {
            return customRedirectUri;
        }

        // Allow exact match against the configured loopback redirect (for CLI flows).
        if (!string.IsNullOrWhiteSpace(_allowedLoopbackRedirect)
            && string.Equals(customRedirectUri, _allowedLoopbackRedirect, StringComparison.Ordinal))
        {
            return customRedirectUri;
        }

        throw new ProblemDetailsException(
            $"The supplied redirect_uri '{customRedirectUri}' is not registered for this provider.");
    }

    private static string GenerateCodeVerifier()
    {
        // RFC 7636 §4.1: 32 random bytes → 43 URL-safe base64 chars (no padding).
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncode(bytes);
    }

    private static string GenerateCodeChallenge(string verifier)
    {
        // S256 method: BASE64URL(SHA256(ASCII(code_verifier)))
        byte[] verifierBytes = Encoding.ASCII.GetBytes(verifier);
        byte[] hash = SHA256.HashData(verifierBytes);
        return Base64UrlEncode(hash);
    }

    private static string GenerateState()
    {
        // 32 bytes → 43 URL-safe base64 chars — exceeds the 32-char minimum.
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncode(bytes);
    }

    private static string GenerateNonce()
    {
        // H6: Per-flow random nonce for OIDC replay protection (RFC 6749 / OIDC Core §3.1.2.1).
        // 32 bytes → 43 URL-safe base64 chars.
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
