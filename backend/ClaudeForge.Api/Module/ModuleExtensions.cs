using System.Reflection;

namespace ClaudeForge.Api.Module;

/// <summary>
/// Reflection-based module auto-discovery. Scans the entry assembly for all
/// <see cref="IModule"/> implementations and registers them.
/// </summary>
public static class ModuleExtensions
{
    public static IServiceCollection RegisterModules(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        foreach (IModule module in DiscoverModules())
        {
            module.RegisterModule(services, configuration);
        }

        return services;
    }

    public static IEndpointRouteBuilder MapModuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        foreach (IModule module in DiscoverModules())
        {
            module.MapEndpoints(endpoints);
        }

        return endpoints;
    }

    private static IEnumerable<IModule> DiscoverModules()
    {
        return Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IModule).IsAssignableFrom(t))
            .Select(Activator.CreateInstance)
            .Cast<IModule>();
    }
}
