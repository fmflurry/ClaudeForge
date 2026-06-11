using ClaudeForge.Infrastructure.Persistence.Seeding;

namespace ClaudeForge.Api.Modules.Seeding;

/// <summary>
/// Hosted service that runs <see cref="IAddOnDataSeeder"/> once at application startup
/// when <c>Features:SeedPlugins</c> is <c>true</c> in the configuration.
/// The seeder is idempotent; running it against a populated database is safe.
/// </summary>
internal sealed class AddOnSeedingHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AddOnSeedingHostedService> _logger;

    public AddOnSeedingHostedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<AddOnSeedingHostedService> logger)
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
            _logger.LogDebug("Features:SeedPlugins is false — skipping plugin data seeding.");
            return;
        }

        _logger.LogInformation("Features:SeedPlugins is true — running AddOnDataSeeder.");

        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        IAddOnDataSeeder seeder = scope.ServiceProvider.GetRequiredService<IAddOnDataSeeder>();

        await seeder.SeedAsync(cancellationToken);

        _logger.LogInformation("AddOnDataSeeder completed successfully.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
