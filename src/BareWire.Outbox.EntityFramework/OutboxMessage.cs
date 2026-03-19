namespace BareWire.Outbox.EntityFramework;

internal sealed class OutboxMessage
{
    public long Id { get; set; }

    public Guid MessageId { get; set; }

    public string DestinationAddress { get; set; } = null!;

    public string ContentType { get; set; } = null!;

    public byte[] Payload { get; set; } = null!;

    public string? Headers { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? DeliveredAt { get; set; }

    public int RetryCount { get; set; }
}
