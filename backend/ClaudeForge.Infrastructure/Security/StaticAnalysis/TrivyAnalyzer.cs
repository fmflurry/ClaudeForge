using System.Text.Json;
using ClaudeForge.Core.Modules.SecurityAnalysis.Ports;
using Microsoft.Extensions.Logging;

namespace ClaudeForge.Infrastructure.Security.StaticAnalysis;

/// <summary>
/// Static analysis adapter for Trivy.
/// Runs <c>trivy fs</c> for dependency vulnerability scanning.
/// Maps Trivy severity levels to our scoring system.
/// </summary>
public sealed class TrivyAnalyzer : BaseToolAnalyzer
{
    public override string ToolName => "trivy";

    protected override string Command => "trivy";

    // Scan filesystem, output JSON, skip updates for offline mode
    protected override string Arguments =>
        "fs --format json --quiet --no-progress --skip-db-update --skip-java-db-update .";

    public TrivyAnalyzer(ILogger<TrivyAnalyzer> logger) : base(logger) { }

    protected override StaticAnalysisResult ParseResult(string jsonOutput)
    {
        if (string.IsNullOrWhiteSpace(jsonOutput))
            return new StaticAnalysisResult(100m, Array.Empty<StaticFinding>());

        try
        {
            using JsonDocument doc = JsonDocument.Parse(jsonOutput);
            JsonElement root = doc.RootElement;

            if (!root.TryGetProperty("Results", out JsonElement results) || results.ValueKind != JsonValueKind.Array)
                return new StaticAnalysisResult(100m, Array.Empty<StaticFinding>());

            var findings = new List<StaticFinding>();

            foreach (JsonElement result in results.EnumerateArray())
            {
                string target = result.TryGetProperty("Target", out JsonElement t) ? t.GetString() ?? "" : "";

                if (!result.TryGetProperty("Vulnerabilities", out JsonElement vulns) || vulns.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (JsonElement vuln in vulns.EnumerateArray())
                {
                    string? pkgName = vuln.TryGetProperty("PkgName", out JsonElement p) ? p.GetString() : null;
                    string? severity = vuln.TryGetProperty("Severity", out JsonElement s) ? s.GetString() : null;
                    string? title = vuln.TryGetProperty("Title", out JsonElement ti) ? ti.GetString() : null;
                    string? vulnId = vuln.TryGetProperty("VulnerabilityID", out JsonElement vid) ? vid.GetString() : null;

                    string mappedSeverity = (severity ?? "").ToLowerInvariant() switch
                    {
                        "critical" => "critical",
                        "high" => "high",
                        "medium" => "medium",
                        "low" => "low",
                        _ => "low"
                    };

                    string message = $"[{vulnId}] {title ?? "Vulnerability detected"} in {pkgName}";
                    findings.Add(new StaticFinding(
                        Severity: mappedSeverity,
                        Message: message,
                        File: target,
                        Line: null));
                }
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
