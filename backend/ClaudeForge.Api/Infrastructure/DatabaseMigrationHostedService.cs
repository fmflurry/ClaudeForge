using ClaudeForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Api.Infrastructure;

/// <summary>
/// Hosted service that applies pending EF Core migrations to the database
/// before any other hosted service (e.g., data seeders) interacts with the schema.
///
/// Registration order matters: this service MUST be registered before any seeding
/// hosted services so that migrations complete before seeders query tables.
///
/// Uses <see cref="IDbContextFactory{TContext}"/> to create a short-lived context
/// that is disposed as soon as migration completes, keeping the migration step
/// isolated from the request-scoped DbContext used by the rest of the application.
/// </summary>
internal sealed class DatabaseMigrationHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DatabaseMigrationHostedService> _logger;

    public DatabaseMigrationHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<DatabaseMigrationHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Applying EF Core migrations…");

        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        MarketplaceDbContext db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();

        await db.Database.MigrateAsync(cancellationToken);

        _logger.LogInformation("EF Core migrations applied successfully.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
