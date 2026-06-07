using System.Collections.Concurrent;
using ClaudeForge.Core.Identity.Ports;

namespace ClaudeForge.Infrastructure.Identity;

/// <summary>
/// In-memory implementation of <see cref="IAuthFlowStatePort"/> backed by a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// Uses an injected <see cref="TimeProvider"/> so tests can advance the clock.
/// </summary>
public sealed class InMemoryAuthFlowStateStore : IAuthFlowStatePort
{
    private readonly ConcurrentDictionary<string, AuthFlowState> _store =
        new(StringComparer.Ordinal);

    private readonly TimeProvider _timeProvider;

    public InMemoryAuthFlowStateStore(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public Task StoreAsync(AuthFlowState entry, CancellationToken ct = default)
    {
        _store[entry.State] = entry;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<AuthFlowState?> ConsumeAsync(string state, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return Task.FromResult<AuthFlowState?>(null);
        }

        if (!_store.TryRemove(state, out AuthFlowState? entry))
        {
            return Task.FromResult<AuthFlowState?>(null);
        }

        // Check expiry using the injected clock.
        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (entry.ExpiresAt <= now)
        {
            return Task.FromResult<AuthFlowState?>(null);
        }

        return Task.FromResult<AuthFlowState?>(entry);
    }
}
