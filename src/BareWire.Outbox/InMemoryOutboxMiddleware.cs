using BareWire.Abstractions.Pipeline;
using Microsoft.Extensions.Logging;

namespace BareWire.Outbox;

internal sealed partial class InMemoryOutboxMiddleware : IMessageMiddleware
{
    private static readonly AsyncLocal<OutboxBuffer?> _current = new();

    private readonly IOutboxStore _outboxStore;
    private readonly ILogger<InMemoryOutboxMiddleware> _logger;

    internal static OutboxBuffer? Current => _current.Value;

    internal InMemoryOutboxMiddleware(IOutboxStore outboxStore, ILogger<InMemoryOutboxMiddleware> logger)
    {
        _outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(MessageContext context, NextMiddleware nextMiddleware)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(nextMiddleware);

        var buffer = new OutboxBuffer();
        _current.Value = buffer;

        try
        {
            await nextMiddleware(context).ConfigureAwait(false);

            if (!buffer.IsEmpty)
            {
                var messages = buffer.GetMessages();
                InMemoryOutboxMiddlewareLogMessages.FlushingBuffer(_logger, context.MessageId, messages.Count);
                await _outboxStore.SaveMessagesAsync(messages, context.CancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            int discardCount = buffer.GetMessages().Count;
            InMemoryOutboxMiddlewareLogMessages.DiscardingBuffer(_logger, context.MessageId, discardCount);
            buffer.Clear();
            throw;
        }
        finally
        {
            _current.Value = null;
        }
    }
}

internal static partial class InMemoryOutboxMiddlewareLogMessages
{
    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Flushing outbox buffer for message {MessageId}: {MessageCount} message(s) to store")]
    internal static partial void FlushingBuffer(
        ILogger logger,
        Guid messageId,
        int messageCount);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Discarding outbox buffer for message {MessageId}: {MessageCount} buffered message(s) lost due to handler exception")]
    internal static partial void DiscardingBuffer(
        ILogger logger,
        Guid messageId,
        int messageCount);
}
