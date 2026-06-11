using ClaudeForge.Api.Module;
using ClaudeForge.Application.Modules.AddOnCatalog.Ports;
using ClaudeForge.Application.Modules.AddOnCatalog.UseCases;
using ClaudeForge.Core.Shared.Authorization;
using ClaudeForge.Core.Shared.Exceptions;
using ClaudeForge.Core.Shared.Model;
using ClaudeForge.Infrastructure.AddOnCatalog;
using ClaudeForge.Infrastructure.Authorization;
using ClaudeForge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Api.Modules.AddOnCatalog;

/// <summary>
/// Feature module for the Plugin Catalog endpoints.
/// Registers all services and maps the three catalog API routes.
/// </summary>
public sealed class AddOnCatalogModule : IModule
{
    public IServiceCollection RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAddOnRepositoryPort>(sp =>
            new AddOnRepositoryAdapter(sp.GetRequiredService<MarketplaceDbContext>()));

        services.AddScoped<ICategoryRepositoryPort>(sp =>
            new AddOnRepositoryAdapter(sp.GetRequiredService<MarketplaceDbContext>()));

        // IAddOnAccessPolicy (singleton — pure logic)
        if (!services.Any(d => d.ServiceType == typeof(IAddOnAccessPolicy)))
        {
            services.AddSingleton<IAddOnAccessPolicy, AddOnAccessPolicy>();
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

        services.AddScoped<ListAddOnsUseCase>();
        services.AddScoped<GetAddOnDetailsUseCase>();
        services.AddScoped<ListCategoriesUseCase>();
        services.AddScoped<GetFeaturedAddOnUseCase>();

        // Warm up the Npgsql connection pool at startup so the first HTTP request
        // does not pay the connection establishment overhead (affects timing tests and latency).
        services.AddHostedService<DbConnectionWarmupHostedService>();

        return services;
    }

    public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/plugins", ListAddOnsHandler)
            .WithName("ListPlugins")
            .WithTags("PluginCatalog");

        endpoints.MapGet("/api/v1/plugins/{pluginId:guid}", GetAddOnByIdHandler)
            .WithName("GetPluginById")
            .WithTags("PluginCatalog");

        endpoints.MapGet("/api/v1/categories", ListCategoriesHandler)
            .WithName("ListCategories")
            .WithTags("PluginCatalog");

        endpoints.MapGet("/api/v1/plugins/featured", GetFeaturedAddOnHandler)
            .WithName("GetFeaturedPlugin")
            .WithTags("PluginCatalog");

        return endpoints;
    }

    private static async Task<IResult> ListAddOnsHandler(
        HttpContext httpContext,
        [FromServices] ListAddOnsUseCase useCase,
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

        ListAddOnsQuery query = new()
        {
            Page = page,
            Limit = limit,
            SortKey = sort,
            SortOrder = order,
            TypeFilter = type,
            LanguageFilter = language,
            UseCaseFilter = useCaseFilter,
        };

        PaginatedEnvelope<AddOnSummaryDto> result = await useCase.ExecuteAsync(query);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetAddOnByIdHandler(
        Guid pluginId,
        [FromServices] GetAddOnDetailsUseCase useCase)
    {
        AddOnDetailDto detail = await useCase.ExecuteAsync(pluginId);
        return Results.Ok(detail);
    }

    private static async Task<IResult> ListCategoriesHandler(
        [FromServices] ListCategoriesUseCase useCase)
    {
        CategoryListDto categories = await useCase.ExecuteAsync();
        return Results.Ok(categories);
    }

    private static async Task<IResult> GetFeaturedAddOnHandler(
        [FromServices] GetFeaturedAddOnUseCase useCase)
    {
        FeaturedAddOnDto? result = await useCase.ExecuteAsync();
        if (result is null)
        {
            return Results.Ok(new { data = (FeaturedAddOnDto?)null });
        }
        return Results.Ok(new { data = result });
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
