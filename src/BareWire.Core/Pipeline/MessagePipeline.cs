using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using BareWire.Abstractions;
using BareWire.Abstractions.Exceptions;
using BareWire.Abstractions.Pipeline;
using BareWire.Abstractions.Serialization;
using BareWire.Abstractions.Transport;
using BareWire.Core.Buffers;
using Microsoft.Extensions.Logging;

namespace BareWire.Core.Pipeline;

// Orchestrates the full inbound and outbound message lifecycle:
// inbound: transport → middleware chain → settlement
// outbound: serialize via pooled writer → OutboundMessage (ready for the bounded publish channel)
internal sealed partial class MessagePipeline
{
    private readonly MiddlewareChain _middlewareChain;
    private readonly ConsumerDispatcher _dispatcher;
    private readonly IMessageDeserializer _deserializer;
    private readonly ILogger<MessagePipeline> _logger;

    internal MessagePipeline(
        MiddlewareChain middlewareChain,
        ConsumerDispatcher dispatcher,
        IMessageDeserializer deserializer,
        ILogger<MessagePipeline> logger)
    {
        _middlewareChain = middlewareChain ?? throw new ArgumentNullException(nameof(middlewareChain));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _deserializer = deserializer ?? throw new ArgumentNullException(nameof(deserializer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // Processes an inbound message through the middleware chain and settles it with the transport.
    // Returns the settlement action that was applied.
    internal async Task<SettlementAction> ProcessInboundAsync(
        InboundMessage message,
        ITransportAdapter adapter,
        IServiceProvider serviceProvider,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        Guid messageId = ParseOrHashMessageId(message.MessageId);

        MessageContext context = new(
            messageId: messageId,
            headers: message.Headers,
            rawBody: message.Body,
            serviceProvider: serviceProvider,
            cancellationToken: ct);

        // The terminator is a no-op placeholder. Higher-level wiring (BareWireBus / receive endpoint
        // configuration) injects the actual typed dispatch via middleware registered before this call.
        // For now the terminator simply completes to allow middleware under test to run end-to-end.
        NextMiddleware terminator = static _ => Task.CompletedTask;

        SettlementAction action;
        try
        {
            await _middlewareChain.InvokeAsync(context, terminator).ConfigureAwait(false);
            action = SettlementAction.Ack;
        }
        catch (UnknownPayloadException ex)
        {
            LogUnknownPayload(_logger, ex, messageId);
            action = SettlementAction.Reject;
        }
        catch (Exception ex)
        {
            LogProcessingError(_logger, ex, messageId);
            action = SettlementAction.Nack;
        }

        await adapter.SettleAsync(action, message, ct).ConfigureAwait(false);
        return action;
    }

    // Serializes a typed message into an OutboundMessage using a pooled buffer.
    // The returned OutboundMessage owns its body bytes (copied from the pool at the serialization boundary).
    // Callers are responsible for routing the OutboundMessage through the publish channel (ADR-006).
    internal static OutboundMessage ProcessOutboundAsync<T>(
        T message,
        IMessageSerializer serializer,
        string routingKey,
        IReadOnlyDictionary<string, string>? headers,
        CancellationToken ct = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(routingKey);

        using PooledBufferWriter writer = new();
        serializer.Serialize(message, writer);

        // Copy from pooled buffer to owned memory at the serialization boundary.
        // The OutboundMessage will outlive the using scope, so we must own the bytes.
        ReadOnlyMemory<byte> body = writer.WrittenMemory.ToArray();

        return new OutboundMessage(
            routingKey: routingKey,
            headers: headers ?? new Dictionary<string, string>(),
            body: body,
            contentType: serializer.ContentType);
    }

    // Converts a transport-provided string message ID to a Guid.
    // If the string is a valid Guid format it is parsed directly.
    // Otherwise a deterministic Guid is derived from the first 16 bytes of the SHA256 hash of the ID string,
    // ensuring we never throw FormatException and always produce a stable, collision-resistant identifier.
    private static Guid ParseOrHashMessageId(string messageId)
    {
        if (Guid.TryParse(messageId, out Guid parsed))
            return parsed;

        byte[] idBytes = Encoding.UTF8.GetBytes(messageId);
        byte[] hash = SHA256.HashData(idBytes);

        // Use the first 16 bytes of the hash as the Guid bytes.
        return new Guid(hash.AsSpan(0, 16));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unknown payload received for message {MessageId}. Rejecting.")]
    private static partial void LogUnknownPayload(ILogger logger, Exception ex, Guid messageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error processing message {MessageId}. Nacking.")]
    private static partial void LogProcessingError(ILogger logger, Exception ex, Guid messageId);
}
