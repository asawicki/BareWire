namespace BareWire.Samples.InboxDeduplication.Data;

// EF Core entity — persisted to PostgreSQL when a PaymentReceived event is consumed.
public sealed class NotificationLog
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string PaymentId { get; init; }
    public required string ConsumerType { get; init; }
    public required string Details { get; init; }
    public DateTime ProcessedAt { get; init; }
}
