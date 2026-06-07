using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Web;
using ClaudeForge.Core.Identity.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace ClaudeForge.Infrastructure.Identity;

/// <summary>
/// OIDC adapter for Microsoft identity (login.microsoftonline.com).
/// </summary>
public sealed class MicrosoftIdentityProviderAdapter : IIdentityProviderPort, INamedIdentityProviderPort
{
    public const string ProviderName = "microsoft";

    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _tenant;
    private readonly HttpClient _httpClient;
    private readonly IOpenIdConfigurationProvider _openIdConfigProvider;
    private readonly ILogger<MicrosoftIdentityProviderAdapter>? _logger;

    string INamedIdentityProviderPort.ProviderName => ProviderName;

    public MicrosoftIdentityProviderAdapter(
        IConfiguration configuration,
        HttpClient httpClient,
        IOpenIdConfigurationProvider openIdConfigProvider,
        ILogger<MicrosoftIdentityProviderAdapter>? logger = null)
    {
        _clientId = configuration["OIDC__MICROSOFT__CLIENTID"]
            ?? throw new InvalidOperationException("OIDC__MICROSOFT__CLIENTID is not configured.");
        _clientSecret = configuration["OIDC__MICROSOFT__CLIENTSECRET"]
            ?? throw new InvalidOperationException("OIDC__MICROSOFT__CLIENTSECRET is not configured.");
        _tenant = configuration["OIDC__MICROSOFT__TENANT"] ?? "common";
        _httpClient = httpClient;
        _openIdConfigProvider = openIdConfigProvider;
        _logger = logger;
    }

    public string BuildAuthorizationUrl(
        string provider,
        string codeChallenge,
        string state,
        string redirectUri,
        string nonce = "")
    {
        string authorizeBase = $"https://login.microsoftonline.com/{_tenant}/oauth2/v2.0/authorize";

        List<string> parts = new()
        {
            "response_type=code",
            $"client_id={HttpUtility.UrlEncode(_clientId)}",
            $"scope={HttpUtility.UrlEncode("openid email profile")}",
            $"redirect_uri={HttpUtility.UrlEncode(redirectUri)}",
            $"code_challenge={HttpUtility.UrlEncode(codeChallenge)}",
            "code_challenge_method=S256",
            $"state={HttpUtility.UrlEncode(state)}",
        };

        // H6: Include nonce when provided so the IdP echoes it back in the id_token.
        if (!string.IsNullOrWhiteSpace(nonce))
        {
            parts.Add($"nonce={HttpUtility.UrlEncode(nonce)}");
        }

        return $"{authorizeBase}?{string.Join("&", parts)}";
    }

    public async Task<string> ExchangeCodeAsync(
        string provider,
        string code,
        string codeVerifier,
        string redirectUri,
        CancellationToken ct = default)
    {
        string tokenEndpoint = $"https://login.microsoftonline.com/{_tenant}/oauth2/v2.0/token";

        FormUrlEncodedContent form = new(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = codeVerifier,
        });

        HttpResponseMessage response = await _httpClient
            .PostAsync(tokenEndpoint, form, ct)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            // H2: Log the upstream error detail server-side; never expose it to the client.
            string body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            _logger?.LogWarning(
                "Microsoft token exchange failed with status {StatusCode}. Response: {Body}",
                (int)response.StatusCode,
                body);
            throw new InvalidOperationException("Code exchange failed.");
        }

        TokenResponse? tokenResponse = await response.Content
            .ReadFromJsonAsync<TokenResponse>(ct)
            .ConfigureAwait(false);

        return tokenResponse?.IdToken
               ?? throw new InvalidOperationException("Microsoft token response did not contain id_token.");
    }

    public async Task<VerifiedIdentity> ValidateIdTokenAsync(
        string provider,
        string rawIdToken,
        CancellationToken ct = default)
    {
        IEnumerable<SecurityKey> signingKeys =
            await _openIdConfigProvider.GetSigningKeysAsync(provider, ct).ConfigureAwait(false);

        string issuer = _openIdConfigProvider.GetIssuer(provider);

        TokenValidationParameters parameters = new()
        {
            ValidIssuer = issuer,
            ValidAudience = _clientId,
            IssuerSigningKeys = signingKeys,
            ValidateLifetime = true,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero,
        };

        // Disable inbound claim-type remapping so that raw JWT claim names (e.g. "sub")
        // are preserved on the ClaimsPrincipal instead of being mapped to long URN names.
        JwtSecurityTokenHandler handler = new()
        {
            MapInboundClaims = false,
        };

        ClaimsPrincipal principal;
        try
        {
            // ValidateToken throws SecurityTokenException subtypes on failure.
            // ArgumentException can be thrown for malformed tokens (invalid Base64Url, etc.);
            // wrap those as SecurityTokenException so callers see a consistent type hierarchy.
            principal = handler.ValidateToken(rawIdToken, parameters, out _);
        }
        catch (SecurityTokenException)
        {
            throw;
        }
        catch (ArgumentException ex)
        {
            throw new SecurityTokenException($"Token validation failed: {ex.Message}", ex);
        }

        string subject = principal.FindFirst("sub")?.Value
            ?? throw new InvalidOperationException("id_token missing 'sub' claim.");
        string email = principal.FindFirst("email")?.Value
            ?? throw new InvalidOperationException("id_token missing 'email' claim.");
        string emailVerifiedRaw = principal.FindFirst("email_verified")?.Value ?? "false";
        bool emailVerified = string.Equals(emailVerifiedRaw, "true", StringComparison.OrdinalIgnoreCase);
        string name = principal.FindFirst("name")?.Value ?? string.Empty;
        // H6: Extract the nonce claim for replay-protection verification by the caller.
        string nonce = principal.FindFirst("nonce")?.Value ?? string.Empty;

        return new VerifiedIdentity(subject, email, emailVerified, name, nonce);
    }

    // ── Internal DTO ─────────────────────────────────────────────────────────────

    private sealed class TokenResponse
    {
        [JsonPropertyName("id_token")]
        public string? IdToken { get; init; }

        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }
    }
}
