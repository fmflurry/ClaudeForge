using ClaudeForge.Core.Identity.Ports;
using Microsoft.Extensions.Configuration;

namespace ClaudeForge.Infrastructure.Identity;

/// <summary>
/// Resolves OIDC provider adapters by name, honoring the OIDC__ENABLEDPROVIDERS allow-list.
/// </summary>
public sealed class IdentityProviderRegistry : IIdentityProviderRegistry
{
    private readonly IReadOnlyDictionary<string, IIdentityProviderPort> _enabledAdapters;

    public IdentityProviderRegistry(
        IConfiguration configuration,
        IEnumerable<IIdentityProviderPort> providers)
    {
        // Build the enabled-provider set from config (comma-separated, case-insensitive)
        string raw = configuration["OIDC__ENABLEDPROVIDERS"] ?? string.Empty;
        HashSet<string> enabledNames = raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim().ToLowerInvariant())
            .Where(s => s.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Index only the adapters whose name appears in the enabled set
        Dictionary<string, IIdentityProviderPort> map = new(StringComparer.OrdinalIgnoreCase);
        foreach (IIdentityProviderPort provider in providers)
        {
            if (provider is INamedIdentityProviderPort named
                && enabledNames.Contains(named.ProviderName))
            {
                map[named.ProviderName] = provider;
            }
        }

        _enabledAdapters = map;
    }

    /// <inheritdoc/>
    public IIdentityProviderPort Resolve(string providerName)
    {
        if (string.IsNullOrEmpty(providerName)
            || !_enabledAdapters.TryGetValue(providerName, out IIdentityProviderPort? adapter))
        {
            throw new UnsupportedProviderException(providerName ?? string.Empty);
        }

        return adapter;
    }
}
