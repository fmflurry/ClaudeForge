using ClaudeForge.Application.Modules.PluginSearch.Ports;
using ClaudeForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClaudeForge.Infrastructure.PluginSearch;

/// <summary>
/// DI registration for plugin search adapters.
/// Reads Features:QdrantEnabled (bool, default false) to select the active adapter.
/// When false  → registers PostgresSearchAdapter as ISearchIndexPort.
/// When true   → registers QdrantSearchAdapter (wrapping PostgresSearchAdapter) as ISearchIndexPort.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPluginSearchAdapters(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string? rawFlag = configuration["Features:QdrantEnabled"];
        bool qdrantEnabled = bool.TryParse(rawFlag, out bool parsed) && parsed;

        if (qdrantEnabled)
        {
            // QdrantSearchAdapter wraps PostgresSearchAdapter as the FTS fallback.
            // We register PostgresSearchAdapter as a named internal dependency via a factory.
            services.AddScoped<ISearchIndexPort>(sp =>
            {
                MarketplaceDbContext context = ResolveOrCreateDbContext(sp);
                PostgresSearchAdapter postgresAdapter = new(context);
                ILogger<QdrantSearchAdapter> logger =
                    sp.GetRequiredService<ILogger<QdrantSearchAdapter>>();

                return new QdrantSearchAdapter(postgresAdapter, logger);
            });
        }
        else
        {
            services.AddScoped<ISearchIndexPort>(sp =>
            {
                MarketplaceDbContext context = ResolveOrCreateDbContext(sp);
                return new PostgresSearchAdapter(context);
            });
        }

        return services;
    }

    /// <summary>
    /// Returns the registered MarketplaceDbContext, or creates a stub instance
    /// (with empty options) for environments where the DB context is not registered
    /// (e.g. unit test DI containers that only test adapter selection logic).
    /// DB operations on a stub context will throw at call time, not at construction time.
    /// </summary>
    private static MarketplaceDbContext ResolveOrCreateDbContext(IServiceProvider sp)
    {
        MarketplaceDbContext? existing = sp.GetService<MarketplaceDbContext>();
        if (existing is not null)
        {
            return existing;
        }

        // Fallback: create a minimal context instance (no DB provider configured).
        // This allows DI resolution in test environments that only verify adapter types.
        DbContextOptions<MarketplaceDbContext> emptyOptions =
            new DbContextOptionsBuilder<MarketplaceDbContext>().Options;

        return new MarketplaceDbContext(emptyOptions);
    }
}
