using ClaudeForge.Api.Infrastructure;
using ClaudeForge.Api.Module;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Auto-discover and register feature modules
builder.Services.RegisterModules();

WebApplication app = builder.Build();

app.UseExceptionHandler();

// Map all module endpoints
app.MapModuleEndpoints();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .WithName("HealthCheck")
    .WithTags("Health");

app.Run();

// Make Program accessible for WebApplicationFactory in integration tests
public partial class Program { }
