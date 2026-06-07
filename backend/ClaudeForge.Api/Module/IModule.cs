using Microsoft.AspNetCore.Routing;

namespace ClaudeForge.Api.Module;

/// <summary>
/// Contract for all feature modules. Modules self-register their services and map their endpoints.
/// Auto-discovered via reflection in <see cref="ModuleExtensions"/>.
/// </summary>
public interface IModule
{
    IServiceCollection RegisterModule(IServiceCollection services, IConfiguration configuration);
    IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints);
}
