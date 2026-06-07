using ClaudeForge.Api.Module;
using ClaudeForge.Application.Modules.PluginCatalog.Ports;
using ClaudeForge.Application.Modules.PluginCatalog.UseCases;
using ClaudeForge.Core.Shared.Authorization;
using ClaudeForge.Core.Shared.Exceptions;
using ClaudeForge.Core.Shared.Model;
using ClaudeForge.Infrastructure.Authorization;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.PluginCatalog;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Api.Modules.PluginCatalog;

/// <summary>
/// Feature module for the Plugin Catalog endpoints.
/// Registers all services and maps the three catalog API routes.
/// </summary>
public sealed class PluginCatalogModule : IModule
{
    public IServiceCollection RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IPluginRepositoryPort>(sp =>
            new PluginRepositoryAdapter(sp.GetRequiredService<MarketplaceDbContext>()));

        services.AddScoped<ICategoryRepositoryPort>(sp =>
            new PluginRepositoryAdapter(sp.GetRequiredService<MarketplaceDbContext>()));

        // IPluginAccessPolicy (singleton — pure logic)
        if (!services.Any(d => d.ServiceType == typeof(IPluginAccessPolicy)))
        {
            services.AddSingleton<IPluginAccessPolicy, PluginAccessPolicy>();
        }

        // IOrgMembershipQueryPort (requires IMemoryCache)
        if (!services.Any(d => d.ServiceType == typeof(IOrgMembershipQueryPort)))
        {
            services.AddMemoryCache();
            services.AddScoped<IOrgMembershipQueryPort>(sp =>
                new OrgMembershipQueryAdapter(
                    sp.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<MarketplaceDbContext>>(),
                    sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>()));
        }

        services.AddScoped<ListPluginsUseCase>();
        services.AddScoped<GetPluginDetailsUseCase>();
        services.AddScoped<ListCategoriesUseCase>();

        // Warm up the Npgsql connection pool at startup so the first HTTP request
        // does not pay the connection establishment overhead (affects timing tests and latency).
        services.AddHostedService<DbConnectionWarmupHostedService>();

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

/// <summary>
/// Hosted service that warms up the Npgsql connection pool at application startup
/// by issuing a lightweight query. This prevents the first HTTP request from paying
/// the TCP connection establishment overhead (~30-40ms), which is particularly
/// important for timing-sensitive tests and production latency.
/// </summary>
internal sealed class DbConnectionWarmupHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DbConnectionWarmupHostedService> _logger;

    public DbConnectionWarmupHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<DbConnectionWarmupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            MarketplaceDbContext ctx = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            // Issue a lightweight query to establish the Npgsql connection pool entry
            await ctx.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            _logger.LogDebug("Database connection pool warmed up successfully.");
        }
        catch (Exception ex)
        {
            // Warmup failure should not prevent the application from starting
            _logger.LogWarning(ex, "Database connection pool warmup failed (non-fatal).");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
