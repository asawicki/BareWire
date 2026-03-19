using System.Collections.Concurrent;

namespace BareWire.Outbox;

internal sealed class InMemoryInboxStore : IInboxStore
{
    private readonly record struct LockKey(Guid MessageId, string ConsumerType);

    // Value is the DateTimeOffset at which the lock expires.
    private readonly ConcurrentDictionary<LockKey, DateTimeOffset> _locks = new();

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
        if (_locks.TryAdd(key, expiry))
        {
            return ValueTask.FromResult(true);
        }

        // Entry exists — check if the existing lock has expired.
        if (_locks.TryGetValue(key, out DateTimeOffset existingExpiry)
            && existingExpiry <= DateTimeOffset.UtcNow)
        {
            // Expired lock: replace it with a fresh one.
            // Use TryUpdate to avoid a race where another thread just refreshed it.
            if (_locks.TryUpdate(key, expiry, existingExpiry))
            {
                return ValueTask.FromResult(true);
            }
        }

        // Active lock held by another processing attempt — this is a duplicate.
        return ValueTask.FromResult(false);
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

        foreach ((LockKey key, DateTimeOffset expiry) in _locks)
        {
            if (expiry <= cutoff)
            {
                _locks.TryRemove(key, out _);
            }
        }

        return ValueTask.CompletedTask;
    }
}
