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
/// </summary>
public sealed class DeviceCodeStore
{
    private readonly ConcurrentDictionary<string, DeviceAuthState> _byDeviceCode =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, string> _userCodeToDeviceCode =
        new(StringComparer.OrdinalIgnoreCase);

    public void Store(DeviceAuthState state)
    {
        _byDeviceCode[state.DeviceCode] = state;
        _userCodeToDeviceCode[state.UserCode] = state.DeviceCode;
    }

    public DeviceAuthState? FindByDeviceCode(string deviceCode)
    {
        return _byDeviceCode.TryGetValue(deviceCode, out DeviceAuthState? state) ? state : null;
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
