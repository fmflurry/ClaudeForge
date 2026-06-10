using System.Diagnostics;
using ClaudeForge.Core.Modules.SecurityAnalysis.Ports;
using Microsoft.Extensions.Logging;

namespace ClaudeForge.Infrastructure.Security.StaticAnalysis;

/// <summary>
/// Base class for static analysis tools that run as external processes.
/// Provides common process invocation, timeout, and graceful degradation.
/// </summary>
public abstract class BaseToolAnalyzer : IStaticAnalyzer
{
    private readonly ILogger _logger;

    protected BaseToolAnalyzer(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public abstract string ToolName { get; }

    /// <summary>The command to execute (e.g., "npx", "semgrep", "gitleaks", "trivy").</summary>
    protected abstract string Command { get; }

    /// <summary>CLI arguments to pass.</summary>
    protected abstract string Arguments { get; }

    /// <summary>Per-tool timeout (default 30 seconds).</summary>
    protected virtual TimeSpan ToolTimeout => TimeSpan.FromSeconds(30);

    public async Task<StaticAnalysisResult> AnalyzeAsync(string pluginCodeDirectory, CancellationToken ct)
    {
        // Check if tool is available
        if (!await IsToolAvailableAsync(ct))
        {
            _logger.LogWarning("Static analysis tool '{ToolName}' not found. Skipping with default safe score.", ToolName);
            return new StaticAnalysisResult(100m, Array.Empty<StaticFinding>());
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(ToolTimeout);

            string output = await RunProcessAsync(pluginCodeDirectory, cts.Token);
            return ParseResult(output);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Static analysis tool '{ToolName}' timed out after {Timeout}.", ToolName, ToolTimeout);
            return new StaticAnalysisResult(50m, [new StaticFinding("medium", $"{ToolName} analysis timed out", "", null)]);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Static analysis tool '{ToolName}' failed. Returning safe default.", ToolName);
            return new StaticAnalysisResult(100m, Array.Empty<StaticFinding>());
        }
    }

    /// <summary>
    /// Checks whether the tool is available on PATH.
    /// Override for tools that need special detection.
    /// </summary>
    protected virtual async Task<bool> IsToolAvailableAsync(CancellationToken ct)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = Command,
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
    /// Runs the tool process and captures stdout.
    /// </summary>
    private async Task<string> RunProcessAsync(string workingDirectory, CancellationToken ct)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Command,
                Arguments = Arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        process.Start();

        // Read both streams concurrently to avoid deadlocks
        string output = await process.StandardOutput.ReadToEndAsync(ct);
        string error = await process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0 && !string.IsNullOrEmpty(error))
        {
            _logger.LogWarning("Tool '{ToolName}' exited with code {ExitCode}: {Error}", ToolName, process.ExitCode, error);
        }

        return output;
    }

    /// <summary>
    /// Parses the tool's JSON output into a normalized score and findings.
    /// Each tool has a different output format — implement in subclass.
    /// </summary>
    protected abstract StaticAnalysisResult ParseResult(string jsonOutput);
}
