namespace BareWire.Outbox;

internal interface IInboxStore
{
    ValueTask<bool> TryLockAsync(
        Guid messageId,
        string consumerType,
        TimeSpan lockTimeout,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an inbox entry as permanently processed, preventing future re-lock attempts
    /// even after the lock expires.
    /// </summary>
    ValueTask MarkProcessedAsync(
        Guid messageId,
        string consumerType,
        CancellationToken cancellationToken = default);

    ValueTask CleanupAsync(
        TimeSpan retention,
        CancellationToken cancellationToken = default);
}
