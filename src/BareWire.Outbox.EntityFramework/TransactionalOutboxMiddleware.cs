using System.Transactions;
using BareWire.Abstractions.Pipeline;
using Microsoft.Extensions.Logging;

namespace BareWire.Outbox.EntityFramework;

internal sealed partial class TransactionalOutboxMiddleware : IMessageMiddleware
{
    // Header key written by RabbitMqHeaderMapper for the message type discriminator.
    private const string MessageTypeHeader = "BW-MessageType";

    private static readonly AsyncLocal<OutboxBuffer?> _current = new();

    private readonly OutboxDbContext _dbContext;
    private readonly IOutboxStore _outboxStore;
    private readonly InboxFilter _inboxFilter;
    private readonly ILogger<TransactionalOutboxMiddleware> _logger;

    internal static OutboxBuffer? Current => _current.Value;

    internal TransactionalOutboxMiddleware(
        OutboxDbContext dbContext,
        IOutboxStore outboxStore,
        InboxFilter inboxFilter,
        ILogger<TransactionalOutboxMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(outboxStore);
        ArgumentNullException.ThrowIfNull(inboxFilter);
        ArgumentNullException.ThrowIfNull(logger);

        _dbContext = dbContext;
        _outboxStore = outboxStore;
        _inboxFilter = inboxFilter;
        _logger = logger;
    }

    public async Task InvokeAsync(MessageContext context, NextMiddleware nextMiddleware)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(nextMiddleware);

        CancellationToken ct = context.CancellationToken;

        // Derive consumer type from EndpointName when available (unique per queue), fall back to
        // BW-MessageType header, then to type name. Using EndpointName prevents two consumers on
        // different queues sharing the same message type from colliding on the inbox key.
        string consumerType = !string.IsNullOrEmpty(context.EndpointName)
            ? context.EndpointName
            : context.Headers.TryGetValue(MessageTypeHeader, out string? headerValue)
                ? headerValue
                : context.GetType().Name;

        // 1. Inbox deduplication check — skip processing if we already have a lock.
        bool lockAcquired = await _inboxFilter
            .TryLockAsync(context.MessageId, consumerType, ct)
            .ConfigureAwait(false);

        if (!lockAcquired)
        {
            TransactionalOutboxLogMessages.DuplicateMessageSkipped(_logger, context.MessageId);
            return;
        }

        var buffer = new OutboxBuffer();
        _current.Value = buffer;

        // 2. Begin ambient transaction — both the application DbContext and OutboxDbContext
        //    must share the same connection string to avoid DTC escalation.
        using var scope = new TransactionScope(
            TransactionScopeOption.Required,
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
            TransactionScopeAsyncFlowOption.Enabled);

        try
        {
            // 3. Invoke handler — business logic runs inside the transaction.
            await nextMiddleware(context).ConfigureAwait(false);

            // 4. Flush outbox buffer — add OutboxMessage entities to DbContext (no SaveChanges yet).
            if (!buffer.IsEmpty)
            {
                var messages = buffer.GetMessages();
                TransactionalOutboxLogMessages.FlushingBuffer(_logger, context.MessageId, messages.Count);
                await _outboxStore.SaveMessagesAsync(messages, ct).ConfigureAwait(false);
            }

            // 5. Atomically persist business state + outbox messages + inbox record.
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

            // 6. Commit the transaction.
            scope.Complete();

            // 7. Mark the inbox entry as permanently processed — prevents re-lock after ExpiresAt.
            //    Runs in a Suppress scope to explicitly opt out of any completed ambient
            //    TransactionScope. This is a standalone best-effort UPDATE; crash between Complete()
            //    and here is an accepted at-least-once risk — consumers must be idempotent.
            using (var suppressScope = new TransactionScope(
                TransactionScopeOption.Suppress,
                TransactionScopeAsyncFlowOption.Enabled))
            {
                await _inboxFilter.MarkProcessedAsync(context.MessageId, consumerType, ct)
                    .ConfigureAwait(false);
                suppressScope.Complete();
            }

            TransactionalOutboxLogMessages.TransactionCompleted(_logger, context.MessageId);
        }
        catch
        {
            // Buffer is discarded; DbContext changes are not saved; TransactionScope
            // disposes without Complete() — automatic rollback.
            int discardCount = buffer.GetMessages().Count;
            TransactionalOutboxLogMessages.DiscardingBuffer(_logger, context.MessageId, discardCount);
            buffer.Clear();
            throw;
        }
        finally
        {
            _current.Value = null;
        }
    }
}

internal static partial class TransactionalOutboxLogMessages
{
    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Flushing transactional outbox buffer for message {MessageId}: {MessageCount} message(s) to store")]
    internal static partial void FlushingBuffer(
        ILogger logger,
        Guid messageId,
        int messageCount);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Discarding transactional outbox buffer for message {MessageId}: {MessageCount} buffered message(s) lost due to handler exception")]
    internal static partial void DiscardingBuffer(
        ILogger logger,
        Guid messageId,
        int messageCount);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Duplicate message {MessageId} skipped by transactional inbox filter")]
    internal static partial void DuplicateMessageSkipped(
        ILogger logger,
        Guid messageId);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Transactional outbox committed successfully for message {MessageId}")]
    internal static partial void TransactionCompleted(
        ILogger logger,
        Guid messageId);
}
