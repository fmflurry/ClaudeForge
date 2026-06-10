using ClaudeForge.Core.Modules.SecurityAnalysis.Services;

namespace ClaudeForge.Core.Modules.SecurityAnalysis.Ports;

/// <summary>
/// Command for persisting a completed analysis result.
/// Contains all data needed to save the result and update the plugin status.
/// Domain-level DTO — referenced by <see cref="ISaveAnalysisResultPort"/> and the Application use case.
/// </summary>
public sealed record SaveAnalysisResultCommand(
    Guid PluginId,
    string PluginVersion,
    CombinedStaticResult? StaticResult,
    DynamicAnalysisResult? DynamicResult,
    ScoreResult Score,
    DecisionResult Decision
);
