namespace ClaudeForge.Core.Modules.Identity.UseCases;

/// <summary>
/// Polls for the result of a device authorization flow (RFC 8628).
/// Returns a discriminated union indicating the current poll state.
///
/// H4 (PARTIAL): Emits <see cref="DeviceTokenPollResult.SlowDown"/> when the client polls
/// faster than the advertised interval. The full /activate browser approval UI is deferred
/// to the CLI device-flow work item.
/// </summary>
public sealed class PollDeviceTokenUseCase
{
    private readonly DeviceCodeStore _store;

    /// <summary>
    /// Advertised polling interval in seconds (must match <see cref="IssueDeviceCodeUseCase"/>).
    /// </summary>
    private const int PollIntervalSeconds = 5;

    public PollDeviceTokenUseCase(DeviceCodeStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Looks up the device code and returns the current authorization state.
    /// </summary>
    public Task<DeviceTokenPollResult> ExecuteAsync(
        string deviceCode,
        CancellationToken ct = default)
    {
        DeviceAuthState? state = _store.FindByDeviceCode(deviceCode);

        if (state is null)
        {
            return Task.FromResult<DeviceTokenPollResult>(new DeviceTokenPollResult.Expired());
        }

        if (state.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _store.Remove(deviceCode);
            return Task.FromResult<DeviceTokenPollResult>(new DeviceTokenPollResult.Expired());
        }

        // H4: Enforce poll interval — return SlowDown if polling too fast.
        bool tooFast = _store.RecordPollAndCheckSlowDown(deviceCode, PollIntervalSeconds);
        if (tooFast)
        {
            return Task.FromResult<DeviceTokenPollResult>(new DeviceTokenPollResult.SlowDown());
        }

        if (state.Tokens is not null)
        {
            // User approved — return tokens and clean up.
            _store.Remove(deviceCode);
            return Task.FromResult<DeviceTokenPollResult>(
                new DeviceTokenPollResult.Approved(state.Tokens));
        }

        return Task.FromResult<DeviceTokenPollResult>(new DeviceTokenPollResult.Pending());
    }
}
