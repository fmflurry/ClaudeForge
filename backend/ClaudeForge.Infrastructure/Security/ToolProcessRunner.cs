using System.ComponentModel;
using System.Diagnostics;

namespace ClaudeForge.Infrastructure.Security;

/// <summary>
/// Utility for running external CLI tools as child processes.
/// Captures stdout/stderr, applies timeout, and handles "command not found" gracefully.
/// </summary>
public static class ToolProcessRunner
{
    /// <summary>
    /// Runs a command with arguments in the specified working directory.
    /// Returns (ExitCode, StdOut, StdErr).
    /// If the command is not found, returns (-1, "", error message) — does not throw.
    /// </summary>
    public static async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(
        string command,
        string arguments,
        string workingDir,
        TimeSpan timeout,
        CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    WorkingDirectory = workingDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.Start();

            // Read both streams concurrently to avoid deadlocks
            string stdOut = await process.StandardOutput.ReadToEndAsync(cts.Token);
            string stdErr = await process.StandardError.ReadToEndAsync(cts.Token);

            await process.WaitForExitAsync(cts.Token);

            return (process.ExitCode, stdOut, stdErr);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Timeout from our linked token, not caller cancellation
            return (-1, string.Empty, $"Process timed out after {timeout.TotalSeconds}s");
        }
        catch (Win32Exception ex)
        {
            // Command not found or failed to start
            return (-1, string.Empty, $"Failed to start process: {ex.Message}");
        }
    }
}
