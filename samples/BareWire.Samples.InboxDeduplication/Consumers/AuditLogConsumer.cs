using BareWire.Abstractions;
using BareWire.Samples.InboxDeduplication.Data;
using BareWire.Samples.InboxDeduplication.Messages;
using Microsoft.Extensions.Logging;

namespace BareWire.Samples.InboxDeduplication.Consumers;

/// <summary>
/// Records an audit log entry for each <see cref="PaymentReceived"/> event.
/// </summary>
/// <remarks>
/// Inbox deduplication uses a composite key (MessageId + ConsumerType), so this consumer
/// processes the same message independently of <see cref="EmailNotificationConsumer"/>.
/// Each consumer gets its own inbox entry — neither blocks the other.
/// </remarks>
public sealed partial class AuditLogConsumer(
    ILogger<AuditLogConsumer> logger,
    NotificationDbContext dbContext) : IConsumer<PaymentReceived>
{
    /// <inheritdoc />
    public async Task ConsumeAsync(ConsumeContext<PaymentReceived> context)
    {
        PaymentReceived message = context.Message;

        LogAuditRecording(logger, message.PaymentId, message.Payer, message.Payee, message.Amount);

        NotificationLog entry = new()
        {
            PaymentId = message.PaymentId,
            ConsumerType = "Audit",
            Details = $"Audit: {message.Payer} paid {message.Amount:C} to {message.Payee} at {message.PaidAt:O}",
            ProcessedAt = DateTime.UtcNow,
        };

        dbContext.NotificationLogs.Add(entry);
        await dbContext.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);

        LogAuditRecorded(logger, message.PaymentId);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Recording audit entry for payment {PaymentId}: {Payer} → {Payee} for {Amount:C}")]
    private static partial void LogAuditRecording(
        ILogger logger, string paymentId, string payer, string payee, decimal amount);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Audit entry persisted for payment {PaymentId}")]
    private static partial void LogAuditRecorded(ILogger logger, string paymentId);
}
