using System.Collections.Concurrent;

namespace BareWire.Outbox;

internal sealed class InMemoryInboxStore : IInboxStore
{
    private readonly record struct LockKey(Guid MessageId, string ConsumerType);

    private readonly record struct LockState(DateTimeOffset ExpiresAt, bool Processed);

    private readonly ConcurrentDictionary<LockKey, LockState> _locks = new();

    public ValueTask<bool> TryLockAsync(
        Guid messageId,
        string consumerType,
        TimeSpan lockTimeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consumerType);
        cancellationToken.ThrowIfCancellationRequested();

        var key = new LockKey(messageId, consumerType);
        DateTimeOffset expiry = DateTimeOffset.UtcNow + lockTimeout;

        // If no entry exists, insert and acquire the lock.
        if (_locks.TryAdd(key, new LockState(expiry, Processed: false)))
        {
            return ValueTask.FromResult(true);
        }

        // Entry exists — check if the existing lock has expired and was not already processed.
        if (_locks.TryGetValue(key, out LockState existingState)
            && existingState.ExpiresAt <= DateTimeOffset.UtcNow
            && !existingState.Processed)
        {
            // Expired and unprocessed lock: replace it with a fresh one.
            // Use TryUpdate to avoid a race where another thread just refreshed it.
            if (_locks.TryUpdate(key, new LockState(expiry, Processed: false), existingState))
            {
                return ValueTask.FromResult(true);
            }
        }

        // Active lock held by another processing attempt — this is a duplicate.
        return ValueTask.FromResult(false);
    }

    public ValueTask MarkProcessedAsync(
        Guid messageId,
        string consumerType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consumerType);
        cancellationToken.ThrowIfCancellationRequested();

        var key = new LockKey(messageId, consumerType);
        _locks.AddOrUpdate(
            key,
            _ => new LockState(DateTimeOffset.UtcNow, Processed: true),
            (_, existing) => existing with { Processed = true });
        return ValueTask.CompletedTask;
    }

    public ValueTask CleanupAsync(
        TimeSpan retention,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Remove entries whose lock was last set more than (retention) ago.
        // Since the expiry stored is (lock acquired time + lockTimeout), entries
        // that expired more than (retention - lockTimeout) ago are eligible.
        // For simplicity: remove all entries whose expiry is older than (now - retention).
        DateTimeOffset cutoff = DateTimeOffset.UtcNow - retention;

        foreach ((LockKey key, LockState state) in _locks)
        {
            if (!state.Processed && state.ExpiresAt <= cutoff)
            {
                _locks.TryRemove(key, out _);
            }
        }

        return ValueTask.CompletedTask;
    }
}
