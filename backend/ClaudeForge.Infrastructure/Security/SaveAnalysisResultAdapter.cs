using System.Text.Json;
using ClaudeForge.Core.Modules.SecurityAnalysis.Ports;
using ClaudeForge.Core.Modules.SecurityAnalysis.Services;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClaudeForge.Infrastructure.Security;

/// <summary>
/// EF Core adapter for <see cref="ISaveAnalysisResultPort"/>.
/// Persists analysis results and updates plugin security score/status.
/// </summary>
public sealed class SaveAnalysisResultAdapter : ISaveAnalysisResultPort
{
    private readonly IDbContextFactory<MarketplaceDbContext> _contextFactory;
    private readonly ILogger<SaveAnalysisResultAdapter> _logger;

    public SaveAnalysisResultAdapter(
        IDbContextFactory<MarketplaceDbContext> contextFactory,
        ILogger<SaveAnalysisResultAdapter> logger)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> SaveAsync(SaveAnalysisResultCommand command, CancellationToken ct = default)
    {
        await using MarketplaceDbContext db = await _contextFactory.CreateDbContextAsync(ct);

        // Build entity from command
        var entity = new AnalysisResultEntity
        {
            Id = Guid.NewGuid(),
            PluginId = command.PluginId,
            PluginVersion = command.PluginVersion,
            StaticEslintScore = command.StaticResult?.EslintScore,
            StaticSemgrepScore = command.StaticResult?.SemgrepScore,
            StaticGitleaksScore = command.StaticResult?.GitleaksScore,
            StaticTrivyScore = command.StaticResult?.TrivyScore,
            StaticFindings = SerializeFindings(command.StaticResult?.AllFindings),
            DynamicBehaviorScore = command.DynamicResult?.BehaviorScore,
            DynamicFindings = SerializeDynamicFindings(command.DynamicResult?.Findings),
            TotalScore = command.Score.TotalScore,
            Status = command.Decision.Status,
            StaticWeight = command.Score.StaticWeight,
            DynamicWeight = command.Score.DynamicWeight,
            PassThreshold = command.Decision.Decision switch
            {
                Decision.Pass => command.Score.TotalScore, // store the actual threshold used
                _ => 80m // default — will be refined in Phase 2.4
            },
            FailThreshold = 50m,
            AnalysisCompletedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        // Use default thresholds from the analysis result entity
        // In production, these would come from a config store
        entity.PassThreshold = 80m;
        entity.FailThreshold = 50m;

        db.AnalysisResults.Add(entity);

        // Update plugin security score and status
        AddOnEntity? plugin = await db.Plugins.FindAsync([command.PluginId], ct);
        if (plugin is not null)
        {
            plugin.SecurityScore = command.Score.TotalScore;
            plugin.SecurityStatus = command.Decision.Status;
        }

        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Saved analysis result {ResultId} for plugin {PluginId} (score: {Score}, status: {Status})",
            entity.Id, command.PluginId, command.Score.TotalScore, command.Decision.Status);

        return entity.Id;
    }

    private static string SerializeFindings(IReadOnlyList<StaticFinding>? findings)
    {
        if (findings is null || findings.Count == 0)
            return "[]";

        var items = findings.Select(f => new
        {
            severity = f.Severity,
            message = f.Message,
            file = f.File,
            line = f.Line
        });

        return JsonSerializer.Serialize(items);
    }

    private static string SerializeDynamicFindings(IReadOnlyList<DynamicFinding>? findings)
    {
        if (findings is null || findings.Count == 0)
            return "[]";

        var items = findings.Select(f => new
        {
            type = f.Type,
            description = f.Description,
            severity = f.Severity
        });

        return JsonSerializer.Serialize(items);
    }
}
