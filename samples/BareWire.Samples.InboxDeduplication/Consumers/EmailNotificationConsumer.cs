using BareWire.Abstractions;
using BareWire.Samples.InboxDeduplication.Data;
using BareWire.Samples.InboxDeduplication.Messages;
using Microsoft.Extensions.Logging;

namespace BareWire.Samples.InboxDeduplication.Consumers;

/// <summary>
/// Simulates sending an email notification for each <see cref="PaymentReceived"/> event.
/// </summary>
/// <remarks>
/// Inbox deduplication is handled transparently by <c>TransactionalOutboxMiddleware</c> —
/// if RabbitMQ redelivers the same message, the middleware prevents this handler from
/// executing a second time. The composite key (MessageId + ConsumerType) ensures that
/// <see cref="AuditLogConsumer"/> can still independently process the same message.
/// </remarks>
public sealed partial class EmailNotificationConsumer(
    ILogger<EmailNotificationConsumer> logger,
    NotificationDbContext dbContext) : IConsumer<PaymentReceived>
{
    /// <inheritdoc />
    public async Task ConsumeAsync(ConsumeContext<PaymentReceived> context)
    {
        PaymentReceived message = context.Message;

        LogEmailSending(logger, message.PaymentId, message.Payer, message.Payee, message.Amount);

        // Simulate email sending delay.
        await Task.Delay(millisecondsDelay: 30, context.CancellationToken).ConfigureAwait(false);

        NotificationLog entry = new()
        {
            PaymentId = message.PaymentId,
            ConsumerType = "Email",
            Details = $"Email notification sent to {message.Payee} for payment {message.Amount:C} from {message.Payer}",
            ProcessedAt = DateTime.UtcNow,
        };

        dbContext.NotificationLogs.Add(entry);
        await dbContext.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);

        LogEmailSent(logger, message.PaymentId);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Sending email notification for payment {PaymentId}: {Payer} → {Payee} for {Amount:C}")]
    private static partial void LogEmailSending(
        ILogger logger, string paymentId, string payer, string payee, decimal amount);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Email notification persisted for payment {PaymentId}")]
    private static partial void LogEmailSent(ILogger logger, string paymentId);
}
