using System.Text.Json;
using ClaudeForge.Core.Modules.SecurityAnalysis.Ports;
using Microsoft.Extensions.Logging;

namespace ClaudeForge.Infrastructure.Security.StaticAnalysis;

/// <summary>
/// Static analysis adapter for Gitleaks.
/// Runs <c>gitleaks detect</c> for secret detection.
/// Any secret finding is high severity and heavily penalizes the score.
/// </summary>
public sealed class GitleaksAnalyzer : BaseToolAnalyzer
{
    public override string ToolName => "gitleaks";

    protected override string Command => "gitleaks";

    // --no-git required when scanning a directory that is not a git repo
    protected override string Arguments => "detect --no-git --source . --report-format json --report-path /dev/stdout";

    public GitleaksAnalyzer(ILogger<GitleaksAnalyzer> logger) : base(logger) { }

    // Gitleaks writes JSON to report-path (stdout) and exits 1 when leaks found
    protected override async Task<bool> IsToolAvailableAsync(CancellationToken ct)
    {
        try
        {
            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = "gitleaks",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                }
            };

            process.Start();
            await process.WaitForExitAsync(ct);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    protected override StaticAnalysisResult ParseResult(string jsonOutput)
    {
        if (string.IsNullOrWhiteSpace(jsonOutput))
            return new StaticAnalysisResult(100m, Array.Empty<StaticFinding>());

        try
        {
            using JsonDocument doc = JsonDocument.Parse(jsonOutput);
            JsonElement root = doc.RootElement;

            var findings = new List<StaticFinding>();

            if (root.ValueKind == JsonValueKind.Array)
            {
                // Gitleaks outputs JSON array of findings
                foreach (JsonElement finding in root.EnumerateArray())
                {
                    string? description = finding.TryGetProperty("Description", out JsonElement desc)
                        ? desc.GetString()
                        : finding.TryGetProperty("Message", out JsonElement msg)
                            ? msg.GetString()
                            : "Secret detected";

                    string? file = finding.TryGetProperty("File", out JsonElement f) ? f.GetString() : "";
                    int? line = finding.TryGetProperty("StartLine", out JsonElement sl)
                        ? sl.GetInt32()
                        : finding.TryGetProperty("Line", out JsonElement ln)
                            ? ln.GetInt32()
                            : null;

                    // Gitleaks findings are always high severity (secrets)
                    findings.Add(new StaticFinding(
                        Severity: "critical",
                        Message: description ?? "Secret detected",
                        File: file ?? "",
                        Line: line));
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
