using ClaudeForge.Api.Infrastructure;
using ClaudeForge.Api.Module;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Built-in .NET 10 OpenAPI document generation (Microsoft.AspNetCore.OpenApi)
builder.Services.AddOpenApi();

// Auto-discover and register feature modules (including rate limiting from PluginPublishingModule)
builder.Services.RegisterModules();

WebApplication app = builder.Build();

app.UseExceptionHandler();

// Serve OpenAPI document at GET /openapi/v1.json (available in all environments)
app.MapOpenApi();

// Rate limiting must be applied before endpoints
app.UseRateLimiter();

// Map all module endpoints
app.MapModuleEndpoints();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .WithName("HealthCheck")
    .WithTags("Health");

app.Run();

// Make Program accessible for WebApplicationFactory in integration tests
public partial class Program { }
