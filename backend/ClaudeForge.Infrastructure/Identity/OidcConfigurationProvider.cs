// Declared in ClaudeForge.Core.Identity.Ports namespace (same as the other port interfaces)
// but physically in the Infrastructure assembly because IOpenIdConfigurationProvider returns
// SecurityKey (Microsoft.IdentityModel.Tokens), a package that lives in Infrastructure.
// Placing it here avoids adding a NuGet dependency to the otherwise lean Core assembly.

using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;

namespace ClaudeForge.Core.Identity.Ports
{
    /// <summary>
    /// Provides OpenID Connect discovery metadata (signing keys + issuer) for a named provider.
    /// In production: wraps <see cref="ConfigurationManager{T}"/> with a 24-hour cache.
    /// In tests: injected as a mock.
    /// </summary>
    public interface IOpenIdConfigurationProvider
    {
        /// <summary>Returns the current signing keys for the named provider.</summary>
        Task<IEnumerable<SecurityKey>> GetSigningKeysAsync(
            string provider,
            CancellationToken ct = default);

        /// <summary>Returns the expected issuer string for the named provider.</summary>
        string GetIssuer(string provider);
    }
}

namespace ClaudeForge.Infrastructure.Identity
{
    using ClaudeForge.Core.Identity.Ports;

    /// <summary>
    /// Production implementation: uses <see cref="ConfigurationManager{OpenIdConnectConfiguration}"/>
    /// for per-provider discovery doc fetching with a 24-hour automatic refresh.
    /// </summary>
    public sealed class OidcConfigurationProvider : IOpenIdConfigurationProvider
    {
        // Keyed by lowercase provider name
        private readonly IReadOnlyDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> _managers;
        private readonly IReadOnlyDictionary<string, string> _issuers;

        public OidcConfigurationProvider(IConfiguration configuration)
        {
            Dictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> managers =
                new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> issuers = new(StringComparer.OrdinalIgnoreCase);

            // Google
            const string googleDiscovery =
                "https://accounts.google.com/.well-known/openid-configuration";
            managers["google"] = BuildManager(googleDiscovery);
            issuers["google"] = "https://accounts.google.com";

            // Microsoft — tenant may vary; use the configured tenant for discovery
            string tenant = configuration["OIDC__MICROSOFT__TENANT"] ?? "common";
            string msDiscovery =
                $"https://login.microsoftonline.com/{tenant}/v2.0/.well-known/openid-configuration";
            managers["microsoft"] = BuildManager(msDiscovery);
            issuers["microsoft"] = $"https://login.microsoftonline.com/{tenant}/v2.0";

            _managers = managers;
            _issuers = issuers;
        }

        public async Task<IEnumerable<SecurityKey>> GetSigningKeysAsync(
            string provider,
            CancellationToken ct = default)
        {
            if (!_managers.TryGetValue(provider, out ConfigurationManager<OpenIdConnectConfiguration>? manager))
                throw new InvalidOperationException(
                    $"No OIDC configuration registered for provider '{provider}'.");

            OpenIdConnectConfiguration config =
                await manager.GetConfigurationAsync(ct).ConfigureAwait(false);
            return config.SigningKeys;
        }

        public string GetIssuer(string provider)
        {
            if (!_issuers.TryGetValue(provider, out string? issuer))
                throw new InvalidOperationException(
                    $"No issuer registered for provider '{provider}'.");

            return issuer;
        }

        private static ConfigurationManager<OpenIdConnectConfiguration> BuildManager(
            string discoveryUrl)
        {
            ConfigurationManager<OpenIdConnectConfiguration> manager = new(
                discoveryUrl,
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever());

            // 24-hour automatic refresh
            manager.AutomaticRefreshInterval = TimeSpan.FromHours(24);
            manager.RefreshInterval = TimeSpan.FromHours(24);
            return manager;
        }
    }
}
