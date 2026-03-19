namespace BareWire.Outbox;

internal enum OutboxEntryStatus
{
    Pending,
    Delivered
}

internal sealed class OutboxEntry
{
    internal long Id { get; init; }
    internal required string RoutingKey { get; init; }
    internal required IReadOnlyDictionary<string, string> Headers { get; init; }

    // Body is rented from ArrayPool<byte>.Shared — caller must return to pool after use.
    internal required byte[] PooledBody { get; init; }
    internal int BodyLength { get; init; }

    internal required string ContentType { get; init; }
    internal DateTimeOffset CreatedAt { get; init; }
    internal DateTimeOffset? DeliveredAt { get; set; }
    internal OutboxEntryStatus Status { get; set; } = OutboxEntryStatus.Pending;
}
