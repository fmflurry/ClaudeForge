using System.Security.Cryptography;
using System.Text;
using ClaudeForge.Core.Identity.Ports;

namespace ClaudeForge.Core.Modules.Identity.UseCases;

/// <summary>
/// Initiates an OAuth/OIDC sign-in flow by building the IdP authorization URL
/// and storing the PKCE state server-side.
/// </summary>
public sealed class InitiateSignInUseCase
{
    private readonly IIdentityProviderRegistry _registry;
    private readonly IAuthFlowStatePort _stateStore;

    /// <summary>Default TTL for stored auth flow state (5 minutes).</summary>
    private static readonly TimeSpan StateTtl = TimeSpan.FromMinutes(5);

    public InitiateSignInUseCase(
        IIdentityProviderRegistry registry,
        IAuthFlowStatePort stateStore)
    {
        _registry = registry;
        _stateStore = stateStore;
    }

    /// <summary>
    /// Resolves the provider, generates PKCE + state, stores the flow state, and returns
    /// the authorization URL. Throws <see cref="UnsupportedProviderException"/> for unknown
    /// providers (which maps to HTTP 400 at the endpoint).
    /// </summary>
    public async Task<InitiateSignInResult> ExecuteAsync(
        string provider,
        string? customRedirectUri,
        CancellationToken ct = default)
    {
        // Resolve provider — throws UnsupportedProviderException for unknown providers.
        IIdentityProviderPort providerPort = _registry.Resolve(provider);

        // Generate PKCE verifier (RFC 7636: 43-128 URL-safe chars).
        string codeVerifier = GenerateCodeVerifier();

        // Generate S256 code challenge: BASE64URL(SHA256(ASCII(verifier))).
        string codeChallenge = GenerateCodeChallenge(codeVerifier);

        // Generate opaque state (min 32 URL-safe chars).
        string state = GenerateState();

        // Determine redirect URI (use custom if provided, else use a placeholder for mock).
        string redirectUri = customRedirectUri ?? string.Empty;

        // Build authorization URL via the provider adapter.
        string authorizationUrl = providerPort.BuildAuthorizationUrl(
            provider, codeChallenge, state, redirectUri);

        // Store auth flow state with TTL.
        AuthFlowState flowState = new(
            State: state,
            CodeVerifier: codeVerifier,
            Provider: provider,
            RedirectUri: redirectUri,
            ExpiresAt: DateTimeOffset.UtcNow.Add(StateTtl));

        await _stateStore.StoreAsync(flowState, ct);

        return new InitiateSignInResult(authorizationUrl, state);
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

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
