using ClaudeForge.Application.Modules.Telemetry.Ports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClaudeForge.Infrastructure.Telemetry;

/// <summary>
/// Nightly background job that purges raw telemetry events older than 90 days.
/// Gated by the <c>Features:TelemetryRetention</c> configuration flag.
/// </summary>
public sealed class TelemetryRetentionJob : BackgroundService
{
    private const int RetentionDays = 90;
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelemetryRetentionJob> _logger;
    private readonly bool _enabled;

    public TelemetryRetentionJob(
        IServiceScopeFactory scopeFactory,
        ILogger<TelemetryRetentionJob> logger,
        bool enabled)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _enabled = enabled;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("TelemetryRetentionJob is disabled (Features:TelemetryRetention=false). Skipping.");
            return;
        }

        _logger.LogInformation("TelemetryRetentionJob started. Retention window: {Days} days.", RetentionDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunPurgeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TelemetryRetentionJob encountered an error during purge.");
            }

            await Task.Delay(RunInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RunPurgeAsync(CancellationToken ct)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        ITelemetryStorePort store = scope.ServiceProvider.GetRequiredService<ITelemetryStorePort>();

        int deleted = await store.PurgeRawEventsOlderThanAsync(RetentionDays, ct);

        _logger.LogInformation(
            "TelemetryRetentionJob purged {DeletedCount} raw telemetry events older than {Days} days.",
            deleted,
            RetentionDays);
    }
}
