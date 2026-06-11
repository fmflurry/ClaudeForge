using ClaudeForge.Api.Module;
using ClaudeForge.Application.Modules.AddOnSearch.Ports;
using ClaudeForge.Application.Modules.AddOnSearch.UseCases;
using ClaudeForge.Core.Shared.Authorization;
using ClaudeForge.Infrastructure.AddOnSearch;
using ClaudeForge.Infrastructure.Authorization;
using ClaudeForge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace ClaudeForge.Api.Modules.AddOnSearch;

/// <summary>
/// Feature module for the Plugin Search and Discovery endpoints.
/// Registers all services and maps three API routes:
///   GET /api/v1/plugins/search  (primary search endpoint)
///   GET /api/v1/search          (alias, delegates to same use-case)
///   GET /api/v1/discovery       (discovery endpoint)
/// </summary>
public sealed class AddOnSearchModule : IModule
{
    public IServiceCollection RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddPluginSearchAdapters(configuration);

        // IOrgMembershipQueryPort (requires IMemoryCache)
        if (!services.Any(d => d.ServiceType == typeof(IOrgMembershipQueryPort)))
        {
            services.AddMemoryCache();
            services.AddScoped<IOrgMembershipQueryPort>(sp =>
                new OrgMembershipQueryAdapter(
                    sp.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<MarketplaceDbContext>>(),
                    sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>()));
        }

        services.AddScoped<SearchAddOnsUseCase>();
        services.AddScoped<DiscoverAddOnsUseCase>();

        return services;
    }

    public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/plugins/search", SearchPluginsHandler)
            .WithName("SearchPlugins")
            .WithTags("PluginSearch");

        endpoints.MapGet("/api/v1/search", SearchPluginsHandler)
            .WithName("SearchPluginsAlias")
            .WithTags("PluginSearch");

        endpoints.MapGet("/api/v1/discovery", DiscoverPluginsHandler)
            .WithName("DiscoverPlugins")
            .WithTags("PluginSearch");

        return endpoints;
    }

    private static async Task<IResult> SearchPluginsHandler(
        [FromServices] SearchAddOnsUseCase useCase,
        [FromQuery] string? q = null,
        [FromQuery(Name = "type")] string[]? type = null,
        [FromQuery(Name = "language")] string[]? language = null,
        [FromQuery(Name = "useCase")] string[]? useCaseFilter = null,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20)
    {
        SearchAddOnsQuery query = new()
        {
            Q = q,
            TypeFilter = type,
            LanguageFilter = language,
            UseCaseFilter = useCaseFilter,
            Page = page,
            Limit = limit,
        };

        SearchAddOnsResult result = await useCase.ExecuteAsync(query);

        // Return a combined response including categorySuggestions alongside the envelope
        return Results.Ok(new SearchResponse(
            result.Envelope.Data,
            result.Envelope.TotalCount,
            result.Envelope.Page,
            result.Envelope.Limit,
            result.Envelope.TotalPages,
            result.CategorySuggestions));
    }

    private static async Task<IResult> DiscoverPluginsHandler(
        [FromServices] DiscoverAddOnsUseCase useCase,
        [FromQuery] string? keyword = null,
        [FromQuery(Name = "language")] string[]? language = null,
        [FromQuery(Name = "useCase")] string[]? useCaseFilter = null,
        [FromQuery(Name = "type")] string[]? type = null)
    {
        DiscoverAddOnsQuery query = new()
        {
            Keyword = keyword,
            LanguageFilter = language,
            UseCaseFilter = useCaseFilter,
            TypeFilter = type,
        };

        DiscoverAddOnsResult result = await useCase.ExecuteAsync(query);

        return Results.Ok(new DiscoveryResponse(result.Items, result.CriteriaEchoed));
    }

    // -------------------------------------------------------------------------
    // Response shapes (anonymous record types for JSON serialization)
    // -------------------------------------------------------------------------

    private sealed record SearchResponse(
        IReadOnlyList<SearchResultDto> Data,
        int TotalCount,
        int Page,
        int Limit,
        int TotalPages,
        IReadOnlyList<string> CategorySuggestions);

    private sealed record DiscoveryResponse(
        IReadOnlyList<DiscoveryResultDto> Items,
        IReadOnlyList<string> CriteriaEchoed);
}
