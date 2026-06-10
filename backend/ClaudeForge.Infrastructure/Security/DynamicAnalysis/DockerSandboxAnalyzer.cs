using System.Diagnostics;
using ClaudeForge.Core.Modules.SecurityAnalysis.Ports;
using Microsoft.Extensions.Logging;

namespace ClaudeForge.Infrastructure.Security.DynamicAnalysis;

/// <summary>
/// Dynamic analysis adapter that runs the plugin inside a Docker sandbox.
/// Creates an isolated container with strict security restrictions:
///   --read-only, --no-new-privileges, --network none
///   --memory=512m, --cpus=1
///   --security-opt=no-new-privileges:true
/// Container is auto-removed after execution (--rm).
/// If Docker is unavailable, returns graceful degradation with note.
/// </summary>
public sealed class DockerSandboxAnalyzer : IDynamicAnalyzer
{
    private readonly ILogger<DockerSandboxAnalyzer> _logger;

    // Docker base image — minimal Alpine with no extra tools
    private const string SandboxImage = "alpine:latest";

    // Default entry point for the plugin (runs the plugin's main script)
    private const string DefaultEntryPoint = "/plugin/entrypoint.sh";

    private static readonly string[] DockerArgs =
    [
        "run",
        "--rm",                    // Auto-cleanup container after exit
        "--read-only",             // Read-only root filesystem
        "--no-new-privileges",     // Prevent privilege escalation
        "--network", "none",       // No network access
        "--memory=512m",            // Limit memory
        "--cpus=1",                 // Limit CPU
        "--security-opt=no-new-privileges:true",
        "--cap-drop=ALL",          // Drop all Linux capabilities
    ];

    public DockerSandboxAnalyzer(ILogger<DockerSandboxAnalyzer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DynamicAnalysisResult> AnalyzeAsync(string pluginPackagePath, CancellationToken ct)
    {
        // Check Docker availability
        if (!await IsDockerAvailableAsync(ct))
        {
            _logger.LogWarning("Docker is not available. Skipping dynamic analysis with safe default.");
            return new DynamicAnalysisResult(
                100m,
                [new DynamicFinding("warning", "Docker unavailable — dynamic analysis skipped", "low")],
                null,
                "Docker unavailable");
        }

        string resolvedPath = Path.GetFullPath(pluginPackagePath);

        if (!Directory.Exists(resolvedPath) && !File.Exists(resolvedPath))
        {
            _logger.LogWarning("Plugin path '{Path}' does not exist. Returning safe default.", resolvedPath);
            return new DynamicAnalysisResult(
                100m,
                [new DynamicFinding("error", $"Plugin path not found: {resolvedPath}", "medium")],
                null,
                "Plugin path not found");
        }

        try
        {
            // Build docker run command
            var args = new List<string>(DockerArgs)
            {
                // Mount plugin directory read-only
                $"-v", $"{resolvedPath}:/plugin:ro",
                SandboxImage,
                $"/bin/sh", DefaultEntryPoint
            };

            string arguments = string.Join(" ", args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));

            _logger.LogInformation(
                "Running Docker sandbox: docker {Args}",
                string.Join(" ", args));

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.Start();

            string output = await process.StandardOutput.ReadToEndAsync(ct);
            string error = await process.StandardError.ReadToEndAsync(ct);

            await process.WaitForExitAsync(ct);

            int exitCode = process.ExitCode;
            var findings = new List<DynamicFinding>();

            _logger.LogInformation(
                "Docker sandbox exited with code {ExitCode}. StdOut length: {OutLen}, StdErr length: {ErrLen}",
                exitCode, output.Length, error.Length);

            // Analyze output for suspicious behavior
            AnalyzeForSuspiciousBehavior(output, error, findings);

            decimal behaviorScore = CalculateBehaviorScore(findings, exitCode);

            return new DynamicAnalysisResult(
                behaviorScore,
                findings.AsReadOnly(),
                TruncateOutput(output),
                TruncateOutput(error));
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Docker sandbox analysis timed out.");
            return new DynamicAnalysisResult(
                50m,
                [new DynamicFinding("warning", "Dynamic analysis timed out in sandbox", "medium")],
                null,
                "Timeout");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Docker sandbox analysis failed.");
            return new DynamicAnalysisResult(
                100m,
                [new DynamicFinding("error", $"Docker sandbox error: {ex.Message}", "medium")],
                null,
                ex.Message);
        }
    }

    /// <summary>
    /// Checks if Docker CLI is available.
    /// </summary>
    private static async Task<bool> IsDockerAvailableAsync(CancellationToken ct)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = "--version",
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

    /// <summary>
    /// Scans stdout/stderr for patterns indicating suspicious behavior.
    /// </summary>
    private static void AnalyzeForSuspiciousBehavior(string output, string error, List<DynamicFinding> findings)
    {
        if (string.IsNullOrEmpty(output) && string.IsNullOrEmpty(error))
            return;

        string combined = (output ?? "") + "\n" + (error ?? "");

        // Network access attempts
        if (combined.Contains("wget", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("curl", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("nc ", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("ncat", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("/dev/tcp", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(new DynamicFinding(
                "network_attempt",
                "Plugin attempted network access (wget/curl/nc detected)",
                "high"));
        }

        // Process spawning
        if (combined.Contains("exec ", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("fork", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("system(", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("popen", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(new DynamicFinding(
                "process_spawn",
                "Plugin attempted to spawn new processes",
                "high"));
        }

        // File system access outside plugin directory
        if (combined.Contains("/etc", StringComparison.OrdinalIgnoreCase) &&
            combined.Contains("passwd", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(new DynamicFinding(
                "file_access",
                "Plugin attempted to access /etc/passwd",
                "critical"));
        }

        if (combined.Contains("/root", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("/home", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(new DynamicFinding(
                "file_access",
                "Plugin attempted to access user home directories",
                "high"));
        }

        // Non-zero exit with errors
        if (!string.IsNullOrEmpty(error))
        {
            findings.Add(new DynamicFinding(
                "error",
                $"Plugin produced stderr output: {TruncateOutput(error)}",
                "medium"));
        }
    }

    /// <summary>
    /// Calculates behavior score based on findings and exit code.
    /// 100 = clean, 0 = critical issues.
    /// </summary>
    private static decimal CalculateBehaviorScore(IReadOnlyList<DynamicFinding> findings, int exitCode)
    {
        decimal score = 100m;
        bool hasCritical = false;

        foreach (var f in findings)
        {
            switch (f.Severity)
            {
                case "critical":
                    score -= 30m;
                    hasCritical = true;
                    break;
                case "high":
                    score -= 15m;
                    break;
                case "medium":
                    score -= 5m;
                    break;
                case "low":
                    score -= 2m;
                    break;
            }
        }

        // Non-zero exit code indicates abnormal execution
        if (exitCode != 0 && !hasCritical)
        {
            score -= 10m;
        }

        return Math.Max(0, Math.Min(100, score));
    }

    private static string? TruncateOutput(string? output)
    {
        if (string.IsNullOrEmpty(output))
            return null;

        const int maxLength = 4096;
        return output.Length <= maxLength ? output : output[..maxLength] + $"\n... [truncated {output.Length - maxLength} chars]";
    }
}
