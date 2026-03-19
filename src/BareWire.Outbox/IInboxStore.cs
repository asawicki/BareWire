namespace BareWire.Outbox;

internal interface IInboxStore
{
    ValueTask<bool> TryLockAsync(
        Guid messageId,
        string consumerType,
        TimeSpan lockTimeout,
        CancellationToken cancellationToken = default);

    ValueTask CleanupAsync(
        TimeSpan retention,
        CancellationToken cancellationToken = default);
}
