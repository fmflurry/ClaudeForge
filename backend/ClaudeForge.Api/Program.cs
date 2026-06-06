using ClaudeForge.Api.Infrastructure;
using ClaudeForge.Api.Module;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Built-in .NET 10 OpenAPI document generation (Microsoft.AspNetCore.OpenApi)
builder.Services.AddOpenApi();

// ── Database: register MarketplaceDbContext with Postgres ────────────────────
builder.Services.AddDbContext<MarketplaceDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

// ── Package Storage: bind options, validate on start, register adapter ──────
builder.Services
    .AddOptions<StorageOptions>()
    .BindConfiguration("PackageStorage")
    .ValidateOnStart();

builder.Services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<StorageOptions>,
    StorageOptionsValidator>();

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
