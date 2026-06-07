using ClaudeForge.Api.Module;
using ClaudeForge.Core.Identity.Ports;
using ClaudeForge.Infrastructure.Identity;
using ClaudeForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClaudeForge.Api.Modules.Identity;

/// <summary>
/// Feature module for Identity: JWKS endpoint, token issuer, refresh-token store,
/// and JWT denylist services.
/// </summary>
public sealed class IdentityModule : IModule
{
    public IServiceCollection RegisterModule(
        IServiceCollection services,
        IConfiguration configuration)
    {
        // ── ITokenIssuerPort ─────────────────────────────────────────────────────
        string? privatePem = configuration["Jwt:PrivatePem"];
        string issuer = configuration["Jwt:Issuer"] ?? "https://claudeforge.io";
        string audience = configuration["Jwt:Audience"] ?? "claudeforge-api";
        string kid = configuration["Jwt:Kid"] ?? "primary";
        int accessTokenMinutes = int.TryParse(configuration["Jwt:AccessTokenMinutes"], out int atm) ? atm : 15;

        if (!string.IsNullOrWhiteSpace(privatePem))
        {
            services.AddSingleton<ITokenIssuerPort>(_ =>
                new RsaTokenIssuerAdapter(
                    privatePem: privatePem,
                    issuer: issuer,
                    audience: audience,
                    accessTokenMinutes: accessTokenMinutes,
                    kid: kid));
        }

        // ── IJwksProvider ────────────────────────────────────────────────────────
        // Build from configured public keys. During key rotation, multiple entries
        // may be present (comma-separated public PEMs with matching kids).
        // If not yet overridden by a test factory, register from config.
        // Only register if not already registered (test factory may override it).
        string? publicPem = configuration["Jwt:PublicPem"];
        if (!string.IsNullOrWhiteSpace(publicPem))
        {
            IReadOnlyList<(string PublicPem, string Kid)> activeKeys =
                [(publicPem, kid)];
            services.AddSingleton<IJwksProvider>(
                _ => new RsaJwksProvider(activeKeys));
        }
        else
        {
            // Fallback: empty key list (will be overridden by test factory or startup validation)
            services.AddSingleton<IJwksProvider>(
                _ => new RsaJwksProvider([]));
        }

        // ── IRefreshTokenStorePort ───────────────────────────────────────────────
        int refreshTokenDays = int.TryParse(configuration["Jwt:RefreshTokenDays"], out int rtd) ? rtd : 30;
        services.AddScoped<IRefreshTokenStorePort>(sp =>
            new RefreshTokenStoreAdapter(
                sp.GetRequiredService<MarketplaceDbContext>(),
                defaultExpiryDays: refreshTokenDays));

        // ── IRevokedJtiStorePort ─────────────────────────────────────────────────
        services.AddScoped<IRevokedJtiStorePort>(sp =>
            new PostgresRevokedJtiStoreAdapter(
                sp.GetRequiredService<MarketplaceDbContext>()));

        // ── Group 4 — OIDC Integration ───────────────────────────────────────────

        // IOpenIdConfigurationProvider (production discovery-doc manager)
        services.AddSingleton<IOpenIdConfigurationProvider>(sp =>
            new OidcConfigurationProvider(sp.GetRequiredService<IConfiguration>()));

        // HttpClient for OIDC adapters (named clients via IHttpClientFactory)
        services.AddHttpClient("oidc-google");
        services.AddHttpClient("oidc-microsoft");

        // Google adapter
        services.AddSingleton<GoogleIdentityProviderAdapter>(sp =>
            new GoogleIdentityProviderAdapter(
                sp.GetRequiredService<IConfiguration>(),
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("oidc-google"),
                sp.GetRequiredService<IOpenIdConfigurationProvider>()));

        // Microsoft adapter
        services.AddSingleton<MicrosoftIdentityProviderAdapter>(sp =>
            new MicrosoftIdentityProviderAdapter(
                sp.GetRequiredService<IConfiguration>(),
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("oidc-microsoft"),
                sp.GetRequiredService<IOpenIdConfigurationProvider>()));

        // Register each adapter as both IIdentityProviderPort and INamedIdentityProviderPort
        services.AddSingleton<IIdentityProviderPort>(sp =>
            sp.GetRequiredService<GoogleIdentityProviderAdapter>());
        services.AddSingleton<INamedIdentityProviderPort>(sp =>
            sp.GetRequiredService<GoogleIdentityProviderAdapter>());
        services.AddSingleton<IIdentityProviderPort>(sp =>
            sp.GetRequiredService<MicrosoftIdentityProviderAdapter>());
        services.AddSingleton<INamedIdentityProviderPort>(sp =>
            sp.GetRequiredService<MicrosoftIdentityProviderAdapter>());

        // IIdentityProviderRegistry
        services.AddSingleton<IIdentityProviderRegistry>(sp =>
            new IdentityProviderRegistry(
                sp.GetRequiredService<IConfiguration>(),
                sp.GetServices<IIdentityProviderPort>()));

        // UserStoreOptions (bind from config section OIDC__USERSTORE__)
        services.Configure<UserStoreOptions>(configuration.GetSection("OIDC__USERSTORE"));

        // IUserStorePort → UserStoreAdapter
        services.AddScoped<IUserStorePort>(sp =>
            new UserStoreAdapter(
                sp.GetRequiredService<IDbContextFactory<MarketplaceDbContext>>(),
                sp.GetRequiredService<IOptions<UserStoreOptions>>()));

        return services;
    }

    public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // JWKS endpoint — public, no authentication required (RFC 7517)
        endpoints.MapGet(
            "/.well-known/jwks.json",
            (IJwksProvider provider) =>
            {
                JwksDocument doc = provider.GetCurrentKeys();
                return Results.Ok(doc);
            })
            .AllowAnonymous()
            .WithName("GetJwks")
            .WithTags("Identity");

        return endpoints;
    }
}
