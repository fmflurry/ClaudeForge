using System.IdentityModel.Tokens.Jwt;
using ClaudeForge.Core.Identity.Ports;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ClaudeForge.Api.Modules.Identity;

/// <summary>
/// Post-configures <see cref="JwtBearerOptions"/> to inject the issuer, audience, and
/// signing key resolver from the DI-registered services at runtime. This allows the test
/// WebApplicationFactory to override both <see cref="IConfiguration"/> and
/// <see cref="IJwksProvider"/> and have JwtBearer validate test-issued tokens correctly.
///
/// Security notes:
///   - RSA keys are resolved once and cached (no per-request native handle leak).
///   - OnTokenValidated checks the jti denylist so revoked access tokens are rejected.
///   - options.Challenge is left at its default so the standard
///     WWW-Authenticate: Bearer header is emitted on 401 (RFC 6750).
/// </summary>
internal sealed class JwksProviderPostConfigureOptions : IPostConfigureOptions<JwtBearerOptions>
{
    private readonly IJwksProvider _jwksProvider;
    private readonly IConfiguration _configuration;

    // Cached resolved keys — IJwksProvider is a singleton and keys are stable until rotation.
    // Built once in PostConfigure; no per-request RSA allocation.
    private IReadOnlyList<SecurityKey>? _cachedKeys;

    public JwksProviderPostConfigureOptions(
        IJwksProvider jwksProvider,
        IConfiguration configuration)
    {
        _jwksProvider = jwksProvider;
        _configuration = configuration;
    }

    public void PostConfigure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        string issuer = _configuration["Jwt:Issuer"] ?? "https://claudeforge.io";
        string audience = _configuration["Jwt:Audience"] ?? "claudeforge-api";

        // Resolve and cache signing keys once (MEDIUM-2: eliminate per-request RSA allocation).
        _cachedKeys = BuildSecurityKeys();

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ValidateIssuerSigningKey = true,
            IssuerSigningKeyResolver = (_, _, _, _) => _cachedKeys,
        };

        // C1: Enforce the jti denylist on access-token validation.
        // Any token whose jti has been revoked (e.g. after signout) is rejected here,
        // making /auth/signout truly invalidate the access token.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                string? jti = context.Principal?
                    .FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

                if (string.IsNullOrWhiteSpace(jti))
                {
                    return;
                }

                IRevokedJtiStorePort? jtiStore = context.HttpContext.RequestServices
                    .GetService<IRevokedJtiStorePort>();

                if (jtiStore is null)
                {
                    return;
                }

                bool isRevoked = await jtiStore.IsRevokedAsync(jti, context.HttpContext.RequestAborted);
                if (isRevoked)
                {
                    context.Fail("Token has been revoked.");
                }
            },
        };

        // MEDIUM-3: Do NOT set options.Challenge = string.Empty.
        // Leave it at the default so WWW-Authenticate: Bearer is emitted on 401 (RFC 6750).
    }

    private IReadOnlyList<SecurityKey> BuildSecurityKeys()
    {
        JwksDocument doc = _jwksProvider.GetCurrentKeys();
        List<SecurityKey> keys = new(doc.Keys.Count);

        foreach (JwksKey key in doc.Keys)
        {
            try
            {
                System.Security.Cryptography.RSA rsa =
                    System.Security.Cryptography.RSA.Create();

                rsa.ImportParameters(new System.Security.Cryptography.RSAParameters
                {
                    Modulus = Base64UrlDecodeBytes(key.N),
                    Exponent = Base64UrlDecodeBytes(key.E),
                });

                keys.Add(new RsaSecurityKey(rsa) { KeyId = key.Kid });
            }
            catch
            {
                // Skip malformed keys gracefully.
            }
        }

        return keys;
    }

    private static byte[] Base64UrlDecodeBytes(string base64Url)
    {
        string padded = base64Url
            .Replace('-', '+')
            .Replace('_', '/');

        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        return Convert.FromBase64String(padded);
    }
}
