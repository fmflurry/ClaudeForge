using ClaudeForge.Api.Module;
using ClaudeForge.Application.Modules.Docs.Ports;
using ClaudeForge.Application.Modules.Docs.UseCases;
using ClaudeForge.Infrastructure.Docs;
using ClaudeForge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace ClaudeForge.Api.Modules.Docs;

/// <summary>
/// Feature module for the Docs API endpoints.
/// Registers all services and maps two API routes:
///   GET /api/v1/docs?search=&amp;page=&amp;limit=  → 200 PaginatedEnvelope&lt;DocSearchResultDto&gt;
///   GET /api/v1/docs/{slug}                → 200 DocPageDto, 404 ProblemDetails for unknown slug
///
/// All documentation is public — no authentication required.
/// Slug route parameter may contain a colon for the "plugin:{slug}" form.
/// </summary>
public sealed class DocsModule : IModule
{
    public IServiceCollection RegisterModule(IServiceCollection services)
    {
        services.AddScoped<IDocsRepositoryPort>(sp =>
            new DocsRepositoryAdapter(sp.GetRequiredService<MarketplaceDbContext>()));

        services.AddScoped<SearchDocsUseCase>();
        services.AddScoped<GetDocPageUseCase>();

        return services;
    }

    public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/docs", SearchDocsHandler)
            .WithName("SearchDocs")
            .WithTags("Docs");

        // Use a catch-all {**slug} pattern so slugs containing ':' (e.g. "plugin:foo") are captured.
        endpoints.MapGet("/api/v1/docs/{**slug}", GetDocBySlugHandler)
            .WithName("GetDocBySlug")
            .WithTags("Docs");

        return endpoints;
    }

    private static async Task<IResult> SearchDocsHandler(
        [FromServices] SearchDocsUseCase useCase,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20)
    {
        SearchDocsQuery query = new()
        {
            Search = search,
            Page = page,
            Limit = limit,
        };

        Core.Shared.Model.PaginatedEnvelope<DocSearchResultDto> envelope =
            await useCase.ExecuteAsync(query);

        return Results.Ok(envelope);
    }

    private static async Task<IResult> GetDocBySlugHandler(
        string slug,
        [FromServices] GetDocPageUseCase useCase)
    {
        DocPageDto page = await useCase.ExecuteAsync(slug);
        return Results.Ok(page);
    }
}
