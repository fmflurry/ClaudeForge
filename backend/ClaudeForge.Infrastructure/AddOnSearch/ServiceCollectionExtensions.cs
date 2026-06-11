using ClaudeForge.Application.Modules.AddOnSearch.Ports;
using ClaudeForge.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClaudeForge.Infrastructure.AddOnSearch;

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

    private static MarketplaceDbContext ResolveOrCreateDbContext(IServiceProvider sp)
    {
        // Hard fail — callers must register a real MarketplaceDbContext (or an in-memory one in tests).
        // The silent stub fallback was removed because it hid misconfiguration silently.
        return sp.GetRequiredService<MarketplaceDbContext>();
    }
}
