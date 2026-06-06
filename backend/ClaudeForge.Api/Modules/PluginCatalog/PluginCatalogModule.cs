using ClaudeForge.Application.Modules.PluginCatalog.Ports;
using ClaudeForge.Application.Modules.PluginCatalog.UseCases;
using ClaudeForge.Api.Module;
using ClaudeForge.Core.Shared.Exceptions;
using ClaudeForge.Core.Shared.Model;
using ClaudeForge.Infrastructure.PluginCatalog;
using ClaudeForge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace ClaudeForge.Api.Modules.PluginCatalog;

/// <summary>
/// Feature module for the Plugin Catalog endpoints.
/// Registers all services and maps the three catalog API routes.
/// </summary>
public sealed class PluginCatalogModule : IModule
{
    public IServiceCollection RegisterModule(IServiceCollection services)
    {
        services.AddScoped<IPluginRepositoryPort>(sp =>
            new PluginRepositoryAdapter(sp.GetRequiredService<MarketplaceDbContext>()));

        services.AddScoped<ICategoryRepositoryPort>(sp =>
            new PluginRepositoryAdapter(sp.GetRequiredService<MarketplaceDbContext>()));

        services.AddScoped<ListPluginsUseCase>();
        services.AddScoped<GetPluginDetailsUseCase>();
        services.AddScoped<ListCategoriesUseCase>();

        return services;
    }

    public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/plugins", ListPluginsHandler)
            .WithName("ListPlugins")
            .WithTags("PluginCatalog");

        endpoints.MapGet("/api/v1/plugins/{pluginId:guid}", GetPluginByIdHandler)
            .WithName("GetPluginById")
            .WithTags("PluginCatalog");

        endpoints.MapGet("/api/v1/categories", ListCategoriesHandler)
            .WithName("ListCategories")
            .WithTags("PluginCatalog");

        return endpoints;
    }

    private static async Task<IResult> ListPluginsHandler(
        HttpContext httpContext,
        [FromServices] ListPluginsUseCase useCase,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string sort = "createdAt",
        [FromQuery] string order = "desc",
        [FromQuery(Name = "type")] string[]? type = null,
        [FromQuery(Name = "language")] string[]? language = null,
        [FromQuery(Name = "useCase")] string[]? useCaseFilter = null)
    {
        // Validate pagination FIRST before any use-case call
        PaginationRequest pagination = new() { Page = page, Limit = limit };
        if (!pagination.IsValid(out string? paginationError))
        {
            return Results.Problem(
                detail: paginationError,
                statusCode: StatusCodes.Status400BadRequest);
        }

        ListPluginsQuery query = new()
        {
            Page = page,
            Limit = limit,
            SortKey = sort,
            SortOrder = order,
            TypeFilter = type,
            LanguageFilter = language,
            UseCaseFilter = useCaseFilter,
        };

        PaginatedEnvelope<PluginSummaryDto> result = await useCase.ExecuteAsync(query);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetPluginByIdHandler(
        Guid pluginId,
        [FromServices] GetPluginDetailsUseCase useCase)
    {
        PluginDetailDto detail = await useCase.ExecuteAsync(pluginId);
        return Results.Ok(detail);
    }

    private static async Task<IResult> ListCategoriesHandler(
        [FromServices] ListCategoriesUseCase useCase)
    {
        CategoryListDto categories = await useCase.ExecuteAsync();
        return Results.Ok(categories);
    }
}
