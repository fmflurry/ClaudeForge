using ClaudeForge.Infrastructure.Persistence.Seeding;

namespace ClaudeForge.Api.Modules.Seeding;

/// <summary>
/// Hosted service that runs <see cref="IDocPageSeeder"/> once at application startup
/// when <c>Features:SeedPlugins</c> is <c>true</c> in the configuration.
/// The seeder is idempotent; running it against a populated database is safe.
/// </summary>
internal sealed class DocPageSeedingHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DocPageSeedingHostedService> _logger;

    public DocPageSeedingHostedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<DocPageSeedingHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        bool seedPlugins = _configuration.GetValue<bool>("Features:SeedPlugins");

        if (!seedPlugins)
        {
            _logger.LogDebug("Features:SeedPlugins is false — skipping doc page seeding.");
            return;
        }

        _logger.LogInformation("Features:SeedPlugins is true — running DocPageSeeder.");

        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        IDocPageSeeder seeder = scope.ServiceProvider.GetRequiredService<IDocPageSeeder>();

        await seeder.SeedAsync(cancellationToken);

        _logger.LogInformation("DocPageSeeder completed successfully.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
