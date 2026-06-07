namespace ClaudeForge.Infrastructure.Identity.Validation;

/// <summary>
/// Configuration options for OIDC providers.
/// Bound from the OIDC__ environment variable section.
/// </summary>
public sealed class OidcOptions
{
    /// <summary>
    /// List of enabled provider names (e.g. ["google", "microsoft"]).
    /// Empty array means no providers are enabled (valid for development).
    /// </summary>
    public string[] EnabledProviders { get; init; } = [];

    /// <summary>
    /// Configuration for the Google OIDC provider.
    /// Required when "google" is in EnabledProviders.
    /// </summary>
    public ProviderConfig? Google { get; init; }

    /// <summary>
    /// Configuration for the Microsoft OIDC provider.
    /// Required when "microsoft" is in EnabledProviders.
    /// </summary>
    public ProviderConfig? Microsoft { get; init; }

    /// <summary>
    /// Allowed loopback redirect URI for development scenarios.
    /// Optional. Example: "http://localhost:5173/auth/callback".
    /// </summary>
    public string? AllowedLoopbackRedirect { get; init; }
}

/// <summary>
/// Configuration for a single OIDC provider.
/// </summary>
public sealed class ProviderConfig
{
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public string? RedirectUri { get; init; }

    /// <summary>
    /// Tenant identifier — used by Microsoft (e.g. "common", "organizations", or a tenant GUID).
    /// </summary>
    public string? Tenant { get; init; }
}

/// <summary>
/// Configuration options for JWT token issuance and validation.
/// Bound from the JWT__ environment variable section.
/// </summary>
public sealed class JwtOptions
{
    public string? Issuer { get; init; }
    public string? Audience { get; init; }

    /// <summary>
    /// RSA private key in PEM format (PKCS#8 "BEGIN PRIVATE KEY" or traditional "BEGIN RSA PRIVATE KEY").
    /// Required for token issuance.
    /// </summary>
    public string? SigningKeyPrivatePem { get; init; }

    /// <summary>Access token lifetime in minutes. Default: 15.</summary>
    public int AccessTokenMinutes { get; init; } = 15;

    /// <summary>Refresh token lifetime in days. Default: 30.</summary>
    public int RefreshTokenDays { get; init; } = 30;
}
