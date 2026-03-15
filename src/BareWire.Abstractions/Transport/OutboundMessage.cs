namespace BareWire.Abstractions.Transport;

/// <summary>
/// Represents a raw message to be sent through the transport layer after serialization.
/// Carries the zero-copy body as a <see cref="ReadOnlyMemory{T}"/> of bytes along with
/// routing and content metadata.
/// </summary>
public sealed class OutboundMessage
{
    /// <summary>
    /// Gets the routing key used to direct the message to the appropriate exchange binding or queue.
    /// </summary>
    public string RoutingKey { get; }

    /// <summary>
    /// Gets the transport-level headers to attach to the outgoing message.
    /// Never null — an empty dictionary is used when no headers are required.
    /// </summary>
    public IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>
    /// Gets the serialized body of the message as a zero-copy <see cref="ReadOnlyMemory{T}"/>.
    /// </summary>
    public ReadOnlyMemory<byte> Body { get; }

    /// <summary>
    /// Gets the MIME content type of the serialized body (e.g. <c>application/json</c>).
    /// </summary>
    public string ContentType { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="OutboundMessage"/> with all required routing and content metadata.
    /// </summary>
    /// <param name="routingKey">The routing key for the message. Must not be null.</param>
    /// <param name="headers">The transport-level headers to attach. Must not be null.</param>
    /// <param name="body">The serialized zero-copy body of the message.</param>
    /// <param name="contentType">The MIME content type of the body. Must not be null.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="routingKey"/>, <paramref name="headers"/>, or
    /// <paramref name="contentType"/> is null.
    /// </exception>
    public OutboundMessage(
        string routingKey,
        IReadOnlyDictionary<string, string> headers,
        ReadOnlyMemory<byte> body,
        string contentType)
    {
        RoutingKey = routingKey ?? throw new ArgumentNullException(nameof(routingKey));
        Headers = headers ?? throw new ArgumentNullException(nameof(headers));
        Body = body;
        ContentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
    }
}
