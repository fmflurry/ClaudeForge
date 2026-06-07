using ClaudeForge.Api.Module;
using ClaudeForge.Application.Modules.Marketplace.Ports;
using ClaudeForge.Application.Modules.Marketplace.UseCases;
using ClaudeForge.Infrastructure.Marketplace;
using ClaudeForge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace ClaudeForge.Api.Modules.Marketplace;

/// <summary>
/// Feature module for Marketplace Stats endpoint:
///   GET /api/v1/stats → 200 MarketplaceStatsDto (anonymous)
///
/// Returns aggregate marketplace statistics only — no per-plugin detail, raw events, or PII.
/// Response is cached for 5 minutes via IMemoryCache.
/// </summary>
public sealed class StatsModule : IModule
{
    public IServiceCollection RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
        // IMemoryCache is already registered by TelemetryModule; AddMemoryCache is idempotent
        // but we call it here so StatsModule is self-contained if Telemetry is ever removed.
        services.AddMemoryCache();

        services.AddScoped<IMarketplaceStatsPort>(sp =>
            new MarketplaceStatsAdapter(sp.GetRequiredService<MarketplaceDbContext>()));

        services.AddScoped<GetMarketplaceStatsUseCase>();

        return services;
    }

    public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/stats", GetMarketplaceStatsHandler)
            .WithName("GetMarketplaceStats")
            .WithTags("Marketplace")
            .AllowAnonymous()
            .Produces<MarketplaceStatsDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return endpoints;
    }

    // =========================================================================
    // Handlers
    // =========================================================================

    private static async Task<IResult> GetMarketplaceStatsHandler(
        [FromServices] GetMarketplaceStatsUseCase useCase,
        CancellationToken ct)
    {
        MarketplaceStatsDto stats = await useCase.ExecuteAsync(ct);
        return Results.Ok(stats);
    }
}
