using System.Text.Json;
using ClaudeForge.Core.Modules.SecurityAnalysis.Ports;
using Microsoft.Extensions.Logging;

namespace ClaudeForge.Infrastructure.Security.StaticAnalysis;

/// <summary>
/// Static analysis adapter for ESLint.
/// Runs <c>npx eslint</c> with security-focused rules.
/// Returns score 100 minus deductions for security rule violations.
/// </summary>
public sealed class EslintAnalyzer : BaseToolAnalyzer
{
    public override string ToolName => "eslint";

    // Use --format=json for machine-readable output
    protected override string Command => "npx";

    protected override string Arguments =>
        "eslint --format=json --rule 'no-eval: error' --rule 'no-implied-eval: error' " +
        "--rule 'no-new-func: error' --rule 'no-unsafe-optional-chaining: error' " +
        "--rule 'no-unsafe-negation: error' --rule 'no-prototype-builtins: error' " +
        "--rule 'no-import-assign: error' --rule 'no-setter-return: error' " +
        "--rule 'no-unsafe-finally: error' --rule 'no-unsafe-optional-chaining: error' " +
        "--rule 'no-unsafe-negation: error' .";

    public EslintAnalyzer(ILogger<EslintAnalyzer> logger) : base(logger) { }

    protected override StaticAnalysisResult ParseResult(string jsonOutput)
    {
        if (string.IsNullOrWhiteSpace(jsonOutput))
            return new StaticAnalysisResult(100m, Array.Empty<StaticFinding>());

        try
        {
            using JsonDocument doc = JsonDocument.Parse(jsonOutput);
            JsonElement root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Array)
                return new StaticAnalysisResult(100m, Array.Empty<StaticFinding>());

            var findings = new List<StaticFinding>();

            foreach (JsonElement fileResult in root.EnumerateArray())
            {
                string filePath = fileResult.TryGetProperty("filePath", out JsonElement fp)
                    ? fp.GetString() ?? ""
                    : "";

                if (!fileResult.TryGetProperty("messages", out JsonElement messages))
                    continue;

                foreach (JsonElement msg in messages.EnumerateArray())
                {
                    int severity = msg.TryGetProperty("severity", out JsonElement sev) ? sev.GetInt32() : 0;
                    string? message = msg.TryGetProperty("message", out JsonElement m) ? m.GetString() : null;
                    int? line = msg.TryGetProperty("line", out JsonElement ln) ? ln.GetInt32() : null;

                    if (string.IsNullOrEmpty(message))
                        continue;

                    findings.Add(new StaticFinding(
                        Severity: severity >= 2 ? "high" : "medium",
                        Message: message!,
                        File: filePath,
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
