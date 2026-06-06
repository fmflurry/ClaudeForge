using System.Text.RegularExpressions;
using ClaudeForge.Application.Modules.Telemetry.Ports;
using ClaudeForge.Core.Shared.Exceptions;

namespace ClaudeForge.Application.Modules.Telemetry.UseCases;

/// <summary>
/// Validates and records a telemetry event.
/// Throws <see cref="ProblemDetailsException"/> (HTTP 400) on invalid input.
/// Does NOT call the store on invalid input.
/// </summary>
public sealed class IngestTelemetryEventUseCase
{
    private static readonly HashSet<string> ValidEventTypes =
        new(StringComparer.Ordinal) { "download", "install" };

    private static readonly Regex HexRegex =
        new("^[0-9a-f]{64}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ITelemetryStorePort _store;

    public IngestTelemetryEventUseCase(ITelemetryStorePort store)
    {
        _store = store;
    }

    public async Task ExecuteAsync(IngestTelemetryCommand cmd, CancellationToken ct = default)
    {
        // Validate EventType first
        if (string.IsNullOrWhiteSpace(cmd.EventType) || !ValidEventTypes.Contains(cmd.EventType))
        {
            throw new ProblemDetailsException("Event type is required and must be 'download' or 'install'.");
        }

        // Validate AnonClientId: non-null/non-whitespace and exactly 64 lowercase hex chars
        if (string.IsNullOrWhiteSpace(cmd.AnonClientId) || !HexRegex.IsMatch(cmd.AnonClientId))
        {
            throw new ProblemDetailsException("Anonymous client ID is required and must be a 64-character hex string.");
        }

        // Validate PluginId
        if (cmd.PluginId == Guid.Empty)
        {
            throw new ProblemDetailsException("Plugin ID is required.");
        }

        TelemetryEvent ev = new()
        {
            EventType = cmd.EventType,
            PluginId = cmd.PluginId,
            Version = cmd.Version,
            AnonClientId = cmd.AnonClientId,
            ClientOs = cmd.ClientOs,
            ClientArch = cmd.ClientArch,
            OccurredAt = DateTimeOffset.UtcNow,
        };

        await _store.RecordEventAsync(ev, ct);
    }
}
