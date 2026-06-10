using System.Text.Json;
using ClaudeForge.Core.Modules.SecurityAnalysis.Ports;
using Microsoft.Extensions.Logging;

namespace ClaudeForge.Infrastructure.Security.StaticAnalysis;

/// <summary>
/// Static analysis adapter for Semgrep.
/// Runs <c>semgrep</c> with security-focused rulesets (auto + security-audit).
/// Returns score 100 minus deductions based on finding severity.
/// </summary>
public sealed class SemgrepAnalyzer : BaseToolAnalyzer
{
    public override string ToolName => "semgrep";

    protected override string Command => "semgrep";

    protected override string Arguments =>
        "scan --config=auto --config=p/security-audit --json .";

    public SemgrepAnalyzer(ILogger<SemgrepAnalyzer> logger) : base(logger) { }

    protected override StaticAnalysisResult ParseResult(string jsonOutput)
    {
        if (string.IsNullOrWhiteSpace(jsonOutput))
            return new StaticAnalysisResult(100m, Array.Empty<StaticFinding>());

        try
        {
            using JsonDocument doc = JsonDocument.Parse(jsonOutput);
            JsonElement root = doc.RootElement;

            if (!root.TryGetProperty("results", out JsonElement results) || results.ValueKind != JsonValueKind.Array)
                return new StaticAnalysisResult(100m, Array.Empty<StaticFinding>());

            var findings = new List<StaticFinding>();

            foreach (JsonElement result in results.EnumerateArray())
            {
                string? checkId = result.TryGetProperty("check_id", out JsonElement cid) ? cid.GetString() : null;
                string? path = result.TryGetProperty("path", out JsonElement p) ? p.GetString() : null;

                int? line = null;
                if (result.TryGetProperty("start", out JsonElement start) &&
                    start.TryGetProperty("line", out JsonElement l))
                {
                    line = l.GetInt32();
                }

                string? message = null;
                string? severity = "medium";
                if (result.TryGetProperty("extra", out JsonElement extra))
                {
                    message = extra.TryGetProperty("message", out JsonElement m) ? m.GetString() : checkId;
                    severity = extra.TryGetProperty("severity", out JsonElement s)
                        ? s.GetString()?.ToLowerInvariant()
                        : "medium";
                }

                // Map semgrep severity to our classification
                string mappedSeverity = (severity ?? "medium") switch
                {
                    "error" => "high",
                    "warning" => "medium",
                    "info" => "low",
                    _ => "medium"
                };

                findings.Add(new StaticFinding(
                    Severity: mappedSeverity,
                    Message: message ?? checkId ?? "Unknown Semgrep finding",
                    File: path ?? "",
                    Line: line));
            }

            decimal score = CalculateScore(findings);
            return new StaticAnalysisResult(score, findings.AsReadOnly());
        }
        catch (JsonException)
        {
            return new StaticAnalysisResult(100m, Array.Empty<StaticFinding>());
        }
    }

    private static decimal CalculateScore(IReadOnlyList<StaticFinding> findings)
    {
        decimal score = 100m;
        foreach (var f in findings)
        {
            score -= f.Severity switch
            {
                "critical" => 25m,
                "high" => 15m,
                "medium" => 8m,
                "low" => 3m,
                _ => 8m
            };
        }
        return Math.Max(0, Math.Min(100, score));
    }
}
