using ClaudeForge.Api.Infrastructure;
using ClaudeForge.Api.Module;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

// ── Upload size limit (H2: 50 MB cap for both Kestrel and multipart) ─────────
const long MaxUploadBytes = 50L * 1024L * 1024L; // 50 MB

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Configure Kestrel request body limit globally
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = MaxUploadBytes;
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Built-in .NET 10 OpenAPI document generation (Microsoft.AspNetCore.OpenApi)
builder.Services.AddOpenApi();

// ── Multipart upload size limit (also enforced at the form reader level) ─────
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = MaxUploadBytes;
});

// ── Database: register MarketplaceDbContext with Postgres ────────────────────
builder.Services.AddDbContext<MarketplaceDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

// IDbContextFactory is required by adapters that manage their own DbContext lifetime
// (UserStoreAdapter, OrgMembershipQueryAdapter). The factory is separate from the
// scoped DbContext registered above and is safe for singleton / parallel use.
builder.Services.AddDbContextFactory<MarketplaceDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")),
    ServiceLifetime.Scoped);

// ── Package Storage: bind options, validate on start, register adapter ──────
builder.Services
    .AddOptions<StorageOptions>()
    .BindConfiguration("PackageStorage")
    .ValidateOnStart();

builder.Services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<StorageOptions>,
    StorageOptionsValidator>();

// ── CORS: restrictive policy scoped to configured origin(s) ─────────────────
// Read allowed origins from configuration ("Cors:AllowedOrigins" as a string array).
// Never use AllowAnyOrigin in production — absence of config defaults to no allowed origins.
string[] allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
        // If no origins configured, the policy allows nothing (safe default).
    });
});

// Auto-discover and register feature modules (including rate limiting from PluginPublishingModule).
// IConfiguration is passed directly — no BuildServiceProvider() anti-pattern.
builder.Services.RegisterModules(builder.Configuration);

WebApplication app = builder.Build();

app.UseExceptionHandler();

// ── Security headers middleware ───────────────────────────────────────────────
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'none'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'";
    await next(context);
});

// ── HSTS in non-development environments ─────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseCors();

// Authentication and authorization middleware must be before endpoint mapping.
app.UseAuthentication();
app.UseAuthorization();

// MEDIUM-5: Rate limiting must run after auth (so authenticated identity is available
// for per-user policies) and before endpoint mapping so all routes are gated.
app.UseRateLimiter();

// Serve OpenAPI document at GET /openapi/v1.json (available in all environments)
app.MapOpenApi();

// Map all module endpoints
app.MapModuleEndpoints();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .WithName("HealthCheck")
    .WithTags("Health");

app.Run();

// Make Program accessible for WebApplicationFactory in integration tests
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public partial class Program { }
