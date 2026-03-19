namespace BareWire.Outbox.EntityFramework;

internal sealed class InboxMessage
{
    public Guid MessageId { get; set; }

    public string ConsumerType { get; set; } = null!;

    public DateTimeOffset ReceivedAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
}
