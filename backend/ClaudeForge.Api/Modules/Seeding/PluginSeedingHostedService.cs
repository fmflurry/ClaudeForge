using ClaudeForge.Infrastructure.Persistence.Seeding;

namespace ClaudeForge.Api.Modules.Seeding;

/// <summary>
/// Hosted service that runs <see cref="IPluginDataSeeder"/> once at application startup
/// when <c>Features:SeedPlugins</c> is <c>true</c> in the configuration.
/// The seeder is idempotent; running it against a populated database is safe.
/// </summary>
internal sealed class PluginSeedingHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PluginSeedingHostedService> _logger;

    public PluginSeedingHostedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<PluginSeedingHostedService> logger)
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

        _logger.LogInformation("Features:SeedPlugins is true — running PluginDataSeeder.");

        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        IPluginDataSeeder seeder = scope.ServiceProvider.GetRequiredService<IPluginDataSeeder>();

        await seeder.SeedAsync(cancellationToken);

        _logger.LogInformation("PluginDataSeeder completed successfully.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
