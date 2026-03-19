using BareWire.Abstractions.Transport;

namespace BareWire.Outbox;

internal interface IOutboxStore
{
    ValueTask SaveMessagesAsync(
        IReadOnlyList<OutboundMessage> messages,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<OutboxEntry>> GetPendingAsync(
        int batchSize,
        CancellationToken cancellationToken = default);

    ValueTask MarkDeliveredAsync(
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken = default);

    ValueTask CleanupAsync(
        TimeSpan retention,
        CancellationToken cancellationToken = default);
}
