namespace ClaudeForge.Core.Modules.Identity.UseCases;

/// <summary>
/// Polls for the result of a device authorization flow (RFC 8628).
/// Returns a discriminated union indicating the current poll state.
/// </summary>
public sealed class PollDeviceTokenUseCase
{
    private readonly DeviceCodeStore _store;

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
