using System.Diagnostics;
using ClaudeForge.Application.Modules.SecurityAnalysis.UseCases;
using ClaudeForge.Core.Modules.Organizations.Ports;
using ClaudeForge.Core.Modules.SecurityAnalysis.Ports;
using ClaudeForge.Core.Modules.SecurityAnalysis.Services;
using ClaudeForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Api.Modules.SecurityAnalysis;

/// <summary>
/// Background worker that polls the PG-based analysis queue and processes jobs.
/// Uses a SemaphoreSlim to limit concurrent processing (default: 2).
/// Implements exponential backoff retry (1s, 2s, 4s, 8s) up to 3 attempts.
/// Polls every 2 seconds when idle, immediately when jobs are available.
/// </summary>
internal sealed class AnalysisWorkerHostedService : IHostedService
{
    private const int MaxConcurrentJobs = 2;
    private const int MaxRetries = 3;
    private static readonly TimeSpan IdlePollInterval = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AnalysisWorkerHostedService> _logger;
    private readonly SemaphoreSlim _semaphore = new(MaxConcurrentJobs, MaxConcurrentJobs);

    private CancellationTokenSource? _cts;
    private Task? _workerTask;

    // Metrics
    private int _jobsProcessed;
    private long _totalProcessingTimeMs;

    public AnalysisWorkerHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<AnalysisWorkerHostedService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _workerTask = ProcessQueueLoopAsync(_cts.Token);
        _logger.LogInformation("Analysis worker started (max concurrent: {MaxConcurrent}, max retries: {MaxRetries})",
            MaxConcurrentJobs, MaxRetries);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Analysis worker stopping...");

        _cts?.Cancel();

        if (_workerTask is not null)
        {
            try
            {
                await _workerTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }
        }

        _logger.LogInformation(
            "Analysis worker stopped. Processed {JobsProcessed} jobs, avg {AvgTime:F0}ms",
            _jobsProcessed,
            _jobsProcessed > 0 ? _totalProcessingTimeMs / _jobsProcessed : 0);
    }

    private async Task ProcessQueueLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _semaphore.WaitAsync(ct);

                // Fire and forget processing — release semaphore in continuation
                _ = ProcessNextJobAsync(ct).ContinueWith(
                    _ => _semaphore.Release(),
                    ct,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in worker loop.");
                await Task.Delay(IdlePollInterval, ct);
            }
        }
    }

    private async Task ProcessNextJobAsync(CancellationToken ct)
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            var queue = scope.ServiceProvider.GetRequiredService<IAnalysisQueue>();

            AnalysisJobDto? job = await queue.DequeueAsync(ct);

            if (job is null)
            {
                // No jobs available — wait before polling again
                await Task.Delay(IdlePollInterval, ct);
                return;
            }

            // Process the job with retry
            await ProcessJobWithRetryAsync(job, scope, ct);
        }
        catch (OperationCanceledException)
        {
            // Shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in job processing loop.");
        }
    }

    private async Task ProcessJobWithRetryAsync(AnalysisJobDto job, IServiceScope scope, CancellationToken ct)
    {
        long startTime = Stopwatch.GetTimestamp();

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await RunSingleAnalysisAsync(job, scope, ct);

                // Success — mark completed
                var queue = scope.ServiceProvider.GetRequiredService<IAnalysisQueue>();
                await queue.MarkCompletedAsync(job.Id, ct);

                Interlocked.Increment(ref _jobsProcessed);
                long elapsed = Stopwatch.GetElapsedTime(startTime).Milliseconds;
                Interlocked.Add(ref _totalProcessingTimeMs, elapsed);

                _logger.LogInformation(
                    "Job {JobId} processed successfully on attempt {Attempt}/{MaxRetries} ({Elapsed}ms)",
                    job.Id, attempt, MaxRetries, elapsed);

                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                TimeSpan delay = RetryDelays[attempt - 1];

                _logger.LogWarning(
                    ex,
                    "Job {JobId} failed on attempt {Attempt}/{MaxRetries}. Retrying in {Delay}...",
                    job.Id, attempt, MaxRetries, delay);

                await Task.Delay(delay, ct);
            }
            catch (Exception ex)
            {
                // Final attempt failed — mark as failed
                var queue = scope.ServiceProvider.GetRequiredService<IAnalysisQueue>();
                await queue.MarkFailedAsync(job.Id, $"All {MaxRetries} attempts failed: {ex.Message}", ct);

                _logger.LogError(ex,
                    "Job {JobId} failed after {MaxRetries} attempts. Marked as failed.",
                    job.Id, MaxRetries);
            }
        }
    }

    /// <summary>
    /// Runs the full analysis pipeline for a single job:
    /// static analysis → dynamic analysis → scoring → decision → save.
    /// </summary>
    private static async Task RunSingleAnalysisAsync(AnalysisJobDto job, IServiceScope scope, CancellationToken ct)
    {
        var staticUseCase = scope.ServiceProvider.GetRequiredService<RunStaticAnalysisUseCase>();
        var dynamicUseCase = scope.ServiceProvider.GetRequiredService<RunDynamicAnalysisUseCase>();
        var scoringEngine = scope.ServiceProvider.GetRequiredService<ScoringEngine>();
        var decisionEngine = scope.ServiceProvider.GetRequiredService<DecisionEngine>();
        var saveUseCase = scope.ServiceProvider.GetRequiredService<SaveAnalysisResultUseCase>();

        // Step 1: Static analysis
        // The plugin code directory is derived from the plugin ID/version
        // In production, this would resolve from package storage
        string pluginCodeDir = Path.Combine(
            Path.GetTempPath(), "claude-forge", "analysis",
            job.PluginId.ToString(), job.PluginVersion);
        Directory.CreateDirectory(pluginCodeDir);

        CombinedStaticResult staticResult = await staticUseCase.ExecuteAsync(pluginCodeDir, ct);

        // Step 2: Dynamic analysis
        string pluginPackagePath = Path.Combine(
            Path.GetTempPath(), "claude-forge", "analysis",
            job.PluginId.ToString(), job.PluginVersion);

        DynamicAnalysisResult dynamicResult = await dynamicUseCase.ExecuteAsync(pluginPackagePath, ct);

        // Step 3: Scoring
        var config = new ScoringConfig
        {
            StaticWeight = 0.6m,
            DynamicWeight = 0.4m,
            PassThreshold = 80m,
            FailThreshold = 50m,
        };

        ScoreResult score = scoringEngine.CalculateScore(staticResult, dynamicResult, config);

        // Step 4: Decision
        DecisionResult decision = decisionEngine.Decide(score.TotalScore, config.PassThreshold, config.FailThreshold);

        // Step 5: Save result
        var command = new SaveAnalysisResultCommand(
            PluginId: job.PluginId,
            PluginVersion: job.PluginVersion,
            StaticResult: staticResult,
            DynamicResult: dynamicResult,
            Score: score,
            Decision: decision);

        await saveUseCase.ExecuteAsync(command, ct);

        // ── Phase 5.1.2: Award karma based on analysis result ──────────────
        IKarmaServicePort karmaService = scope.ServiceProvider.GetRequiredService<IKarmaServicePort>();
        Guid? authorId = await GetAddOnAuthorIdAsync(scope, job.PluginId, ct);

        if (authorId.HasValue)
        {
            switch (decision.Status)
            {
                case "passed":
                    await karmaService.AddKarmaAsync(
                        authorId.Value, 50, "analysis_passed",
                        $"Plugin {job.PluginId} passed security analysis (score: {score.TotalScore:F1})",
                        ct);

                    // ── Phase 5.5.1: Auto-approval for high-karma authors ──
                    await TryAutoApproveForSafeZoneAsync(scope, job, authorId.Value, ct);
                    break;

                case "failed":
                    await karmaService.AddKarmaAsync(
                        authorId.Value, -20, "analysis_failed",
                        $"Plugin {job.PluginId} failed security analysis (score: {score.TotalScore:F1})",
                        ct);
                    break;

                case "in_review":
                    await karmaService.AddKarmaAsync(
                        authorId.Value, 5, "analysis_review",
                        $"Plugin {job.PluginId} requires manual review (score: {score.TotalScore:F1})",
                        ct);
                    break;
            }
        }

        // Notify author placeholder — log the decision
        // Phase 2.4.5: Notify author — placeholder for now
        System.Diagnostics.Debug.WriteLine(
            $"[NOTIFY] Plugin {job.PluginId} version {job.PluginVersion}: {decision.Status} (score: {score.TotalScore:F1})");
    }

    /// <summary>
    /// Gets the plugin author's user ID.
    /// Uses OwnerUserId from the plugins table.
    /// </summary>
    private static async Task<Guid?> GetAddOnAuthorIdAsync(IServiceScope scope, Guid pluginId, CancellationToken ct)
    {
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        var plugin = await db.Plugins
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == pluginId, ct);

        if (plugin is null)
            return null;

        if (plugin.OwnerUserId.HasValue)
            return plugin.OwnerUserId.Value;

        if (Guid.TryParse(plugin.Author, out Guid parsed))
            return parsed;

        return null;
    }

    /// <summary>
    /// Auto-approval (5.5.1): If the author's karma >= auto_approve_karma_threshold (200),
    /// automatically add the plugin to the org's safe zone.
    /// </summary>
    private static async Task TryAutoApproveForSafeZoneAsync(IServiceScope scope, AnalysisJobDto job, Guid authorId, CancellationToken ct)
    {
        const int autoApproveKarmaThreshold = 200;

        IKarmaServicePort karmaService = scope.ServiceProvider.GetRequiredService<IKarmaServicePort>();
        KarmaSummary summary = await karmaService.GetKarmaAsync(authorId, ct);

        if (summary.KarmaPoints < autoApproveKarmaThreshold)
            return;

        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();

        // Get the plugin to check OwnerOrgId
        var plugin = await db.Plugins
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == job.PluginId, ct);

        if (plugin?.OwnerOrgId is null || plugin.OwnerOrgId.Value == Guid.Empty)
            return;

        // Check if already approved
        bool alreadyApproved = await db.SafeZonePlugins
            .AnyAsync(sz => sz.OrgId == plugin.OwnerOrgId.Value
                         && sz.PluginId == job.PluginId
                         && sz.PluginVersion == job.PluginVersion
                         && sz.IsActive, ct);

        if (alreadyApproved)
            return;

        // Auto-approve for the plugin's owner org
        var entry = new ClaudeForge.Infrastructure.Persistence.Entities.SafeZoneAddOnEntity
        {
            Id = Guid.NewGuid(),
            OrgId = plugin.OwnerOrgId.Value,
            PluginId = job.PluginId,
            PluginVersion = job.PluginVersion,
            ApprovedBy = authorId, // Marked as auto-approved by author ID
            ApprovedAt = DateTimeOffset.UtcNow,
            IsActive = true,
        };

        db.SafeZonePlugins.Add(entry);
        await db.SaveChangesAsync(ct);

        // Award small karma for auto-approval
        await karmaService.AddKarmaAsync(
            authorId, 5, "auto_approved",
            $"Plugin {job.PluginId} auto-approved for safe zone (karma >= {autoApproveKarmaThreshold})",
            ct);
    }
}
