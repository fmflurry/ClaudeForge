using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using ClaudeForge.Core.Identity.Ports;

namespace ClaudeForge.Core.Modules.Identity.UseCases;

/// <summary>
/// In-memory state for a pending device authorization (RFC 8628).
/// </summary>
public sealed record DeviceAuthState(
    string DeviceCode,
    string UserCode,
    string Provider,
    DateTimeOffset ExpiresAt,
    SignInTokens? Tokens = null);

/// <summary>
/// Global in-memory store for device authorization states.
/// Singleton — shared across all use case instances.
///
/// H4 (PARTIAL): Capped at <see cref="MaxEntries"/> to prevent unbounded growth from
/// abandoned device codes. Expired entries are swept on each <see cref="Store"/> call.
/// The full /activate browser approval UI is deferred to the CLI device-flow work item.
/// </summary>
public sealed class DeviceCodeStore
{
    /// <summary>
    /// Maximum number of live (unexpired) device code entries before new ones are rejected.
    /// Prevents unbounded memory growth from abandoned codes.
    /// </summary>
    private const int MaxEntries = 10_000;

    private readonly ConcurrentDictionary<string, DeviceAuthState> _byDeviceCode =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, string> _userCodeToDeviceCode =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Per-device-code last-poll timestamp — used to enforce the advertised poll interval
    /// and return SlowDown when clients poll faster than the interval.
    /// </summary>
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastPollTime =
        new(StringComparer.Ordinal);

    public void Store(DeviceAuthState state)
    {
        // Sweep expired entries first to reclaim space before checking the cap.
        SweepExpired();

        if (_byDeviceCode.Count >= MaxEntries)
        {
            throw new InvalidOperationException(
                "Device code store is at capacity. Please retry later.");
        }

        _byDeviceCode[state.DeviceCode] = state;
        _userCodeToDeviceCode[state.UserCode] = state.DeviceCode;
    }

    public DeviceAuthState? FindByDeviceCode(string deviceCode)
    {
        return _byDeviceCode.TryGetValue(deviceCode, out DeviceAuthState? state) ? state : null;
    }

    /// <summary>
    /// Looks up a device authorization state by user code (case-insensitive).
    /// Returns <c>null</c> when <paramref name="userCode"/> is null, empty, or unknown.
    /// </summary>
    public DeviceAuthState? FindByUserCode(string? userCode)
    {
        if (string.IsNullOrEmpty(userCode))
        {
            return null;
        }

        if (!_userCodeToDeviceCode.TryGetValue(userCode, out string? deviceCode))
        {
            return null;
        }

        return FindByDeviceCode(deviceCode);
    }

    public void Update(DeviceAuthState state)
    {
        _byDeviceCode[state.DeviceCode] = state;
    }

    public void Remove(string deviceCode)
    {
        if (_byDeviceCode.TryRemove(deviceCode, out DeviceAuthState? state))
        {
            _userCodeToDeviceCode.TryRemove(state.UserCode, out _);
        }

        _lastPollTime.TryRemove(deviceCode, out _);
    }

    /// <summary>
    /// Records a poll attempt for the given device code and returns whether the poll is
    /// arriving faster than the advertised <paramref name="intervalSeconds"/>.
    /// Returns <c>true</c> when the client should slow down.
    /// </summary>
    public bool RecordPollAndCheckSlowDown(string deviceCode, int intervalSeconds)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (_lastPollTime.TryGetValue(deviceCode, out DateTimeOffset lastPoll))
        {
            if ((now - lastPoll).TotalSeconds < intervalSeconds)
            {
                // Do not update lastPoll on a rejected (too-fast) attempt — RFC 8628 §3.5.
                return true;
            }
        }

        _lastPollTime[deviceCode] = now;
        return false;
    }

    private void SweepExpired()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach (KeyValuePair<string, DeviceAuthState> kvp in _byDeviceCode)
        {
            if (kvp.Value.ExpiresAt <= now)
            {
                Remove(kvp.Key);
            }
        }
    }
}

/// <summary>
/// Issues a device authorization code per RFC 8628.
/// Throws <see cref="UnsupportedProviderException"/> for unknown providers (→ HTTP 400).
/// </summary>
public sealed class IssueDeviceCodeUseCase
{
    private readonly IIdentityProviderRegistry _registry;
    private readonly DeviceCodeStore _store;
    private readonly string _issuer;

    private const int DeviceCodeTtlSeconds = 900; // 15 minutes
    private const int PollIntervalSeconds = 5;
    private const int UserCodeLength = 8;

    public IssueDeviceCodeUseCase(
        IIdentityProviderRegistry registry,
        DeviceCodeStore store,
        string issuer)
    {
        _registry = registry;
        _store = store;
        _issuer = issuer;
    }

    /// <summary>
    /// Validates the provider and issues a device code response.
    /// </summary>
    public Task<DeviceCodeResponse> ExecuteAsync(string provider, CancellationToken ct = default)
    {
        // Validate provider — throws UnsupportedProviderException → 400.
        _registry.Resolve(provider);

        string deviceCode = GenerateDeviceCode();
        string userCode = GenerateUserCode();
        string verificationUrl = $"{_issuer.TrimEnd('/')}/activate";

        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddSeconds(DeviceCodeTtlSeconds);

        DeviceAuthState state = new(
            DeviceCode: deviceCode,
            UserCode: userCode,
            Provider: provider,
            ExpiresAt: expiresAt);

        _store.Store(state);

        return Task.FromResult(new DeviceCodeResponse(
            DeviceCode: deviceCode,
            UserCode: userCode,
            VerificationUrl: verificationUrl,
            ExpiresIn: DeviceCodeTtlSeconds,
            Interval: PollIntervalSeconds));
    }

    private static string GenerateDeviceCode()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string GenerateUserCode()
    {
        // 8 uppercase alphanumeric chars.
        const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        StringBuilder sb = new(UserCodeLength);
        for (int i = 0; i < UserCodeLength; i++)
        {
            sb.Append(Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]);
        }
        return sb.ToString();
    }
}
