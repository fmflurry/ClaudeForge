using ClaudeForge.Api.Module;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Seeding;

namespace ClaudeForge.Api.Modules.Seeding;

/// <summary>
/// Feature module that registers the plugin data seeder and, when
/// <c>Features:SeedPlugins</c> is <c>true</c>, runs it at startup via a hosted service.
/// </summary>
public sealed class SeedingModule : IModule
{
    public IServiceCollection RegisterModule(IServiceCollection services)
    {
        // Register seeders so they can be resolved from DI (e.g., in tests or hosted services)
        services.AddScoped<ICategorySeeder>(sp =>
            new CategorySeeder(sp.GetRequiredService<MarketplaceDbContext>()));

        services.AddScoped<IPluginDataSeeder>(sp =>
            new PluginDataSeeder(
                sp.GetRequiredService<MarketplaceDbContext>(),
                sp.GetRequiredService<ICategorySeeder>()));

        // Register the hosted service that seeds on startup when the flag is enabled
        services.AddHostedService<PluginSeedingHostedService>();

        return services;
    }

    public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // No endpoints exposed by this module
        return endpoints;
    }
}
