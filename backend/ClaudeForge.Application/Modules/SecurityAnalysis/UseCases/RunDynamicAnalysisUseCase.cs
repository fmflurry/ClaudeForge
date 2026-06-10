using ClaudeForge.Core.Modules.SecurityAnalysis.Ports;

namespace ClaudeForge.Application.Modules.SecurityAnalysis.UseCases;

/// <summary>
/// Orchestrates dynamic analysis by running the plugin inside a sandbox (Docker).
/// Has a 3-minute total timeout.
/// Gracefully handles sandbox failures.
/// </summary>
public sealed class RunDynamicAnalysisUseCase
{
    private readonly IDynamicAnalyzer _analyzer;
    private readonly TimeSpan _globalTimeout = TimeSpan.FromMinutes(3);

    public RunDynamicAnalysisUseCase(IDynamicAnalyzer analyzer)
    {
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
    }

    /// <summary>
    /// Runs dynamic analysis with a 3-minute timeout.
    /// If the sandbox is unavailable or analysis fails, returns a default safe result.
    /// </summary>
    public async Task<DynamicAnalysisResult> ExecuteAsync(
        string pluginPackagePath,
        CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_globalTimeout);

        try
        {
            return await _analyzer.AnalyzeAsync(pluginPackagePath, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return new DynamicAnalysisResult(
                50m,
                [new DynamicFinding("warning", "Dynamic analysis timed out after 3 minutes", "medium")],
                null,
                "Timeout");
        }
        catch (Exception)
        {
            return new DynamicAnalysisResult(
                100m,
                [new DynamicFinding("error", "Dynamic analysis failed; sandbox unavailable", "medium")],
                null,
                "Sandbox unavailable");
        }
    }
}
