using System.Buffers;
using BareWire.Abstractions;

namespace BareWire.Saga;

/// <summary>
/// A concrete <see cref="ConsumeContext"/> used when dispatching messages to saga state machines.
/// Provides access to publish and send capabilities so that saga activities can emit side-effects.
/// </summary>
internal sealed class SagaDispatchConsumeContext(
    Guid messageId,
    Guid? correlationId,
    Guid? conversationId,
    Uri? sourceAddress,
    Uri? destinationAddress,
    DateTimeOffset? sentTime,
    IReadOnlyDictionary<string, string> headers,
    string? contentType,
    ReadOnlySequence<byte> rawBody,
    IPublishEndpoint publishEndpoint,
    ISendEndpointProvider sendEndpointProvider,
    CancellationToken cancellationToken = default)
    : ConsumeContext(
        messageId, correlationId, conversationId,
        sourceAddress, destinationAddress, sentTime,
        headers, contentType, rawBody,
        publishEndpoint, sendEndpointProvider,
        cancellationToken);
