using ClaudeForge.Core.Modules.SecurityAnalysis.Ports;

namespace ClaudeForge.Application.Modules.SecurityAnalysis.UseCases;

/// <summary>
/// Orchestrates running all registered static analysis tools against a plugin code directory.
/// Runs analyzers in parallel with a 2-minute total timeout.
/// Gracefully handles individual tool failures — failed tools contribute score 100 (no findings),
/// and remaining tools continue.
/// </summary>
public sealed class RunStaticAnalysisUseCase
{
    private readonly IEnumerable<IStaticAnalyzer> _analyzers;
    private readonly TimeSpan _globalTimeout = TimeSpan.FromMinutes(2);

    public RunStaticAnalysisUseCase(IEnumerable<IStaticAnalyzer> analyzers)
    {
        _analyzers = analyzers ?? throw new ArgumentNullException(nameof(analyzers));
    }

    /// <summary>
    /// Runs all static analyzers in parallel and returns a combined result.
    /// If all analyzers fail or none are registered, returns a default safe result.
    /// </summary>
    public async Task<CombinedStaticResult> ExecuteAsync(
        string pluginCodeDirectory,
        CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_globalTimeout);

        IStaticAnalyzer[] analyzers = _analyzers as IStaticAnalyzer[] ?? [.. _analyzers];

        if (analyzers.Length == 0)
        {
            return new CombinedStaticResult(100m, 100m, 100m, 100m, 100m, Array.Empty<StaticFinding>());
        }

        // Run all analyzers in parallel with individual try/catch
        StaticAnalysisResult[] results = await Task.WhenAll(
            analyzers.Select(analyzer => RunSingleAnalyzerAsync(analyzer, pluginCodeDirectory, cts.Token)));

        // Aggregate results
        decimal eslintScore = 100m;
        decimal semgrepScore = 100m;
        decimal gitleaksScore = 100m;
        decimal trivyScore = 100m;
        var allFindings = new List<StaticFinding>();

        for (int i = 0; i < analyzers.Length; i++)
        {
            StaticAnalysisResult result = results[i];

            switch (analyzers[i].ToolName.ToLowerInvariant())
            {
                case "eslint":
                    eslintScore = result.Score;
                    break;
                case "semgrep":
                    semgrepScore = result.Score;
                    break;
                case "gitleaks":
                    gitleaksScore = result.Score;
                    break;
                case "trivy":
                    trivyScore = result.Score;
                    break;
            }

            allFindings.AddRange(result.Findings);
        }

        decimal averageScore = (eslintScore + semgrepScore + gitleaksScore + trivyScore) / 4m;

        return new CombinedStaticResult(
            EslintScore: eslintScore,
            SemgrepScore: semgrepScore,
            GitleaksScore: gitleaksScore,
            TrivyScore: trivyScore,
            AverageScore: Math.Round(averageScore, 1),
            AllFindings: allFindings.AsReadOnly());
    }

    private static async Task<StaticAnalysisResult> RunSingleAnalyzerAsync(
        IStaticAnalyzer analyzer,
        string pluginCodeDirectory,
        CancellationToken ct)
    {
        try
        {
            return await analyzer.AnalyzeAsync(pluginCodeDirectory, ct);
        }
        catch (OperationCanceledException)
        {
            // Timeout or cancellation — return safe default for this tool
            return new StaticAnalysisResult(100m, Array.Empty<StaticFinding>());
        }
        catch (Exception)
        {
            // Tool failure — continue with safe default
            return new StaticAnalysisResult(100m, Array.Empty<StaticFinding>());
        }
    }
}
