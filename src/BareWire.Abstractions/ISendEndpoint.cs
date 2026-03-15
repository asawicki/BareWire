namespace BareWire.Abstractions;

/// <summary>
/// Represents a point-to-point send endpoint targeting a specific queue or address.
/// Implementations are thread-safe and may be called concurrently.
/// </summary>
public interface ISendEndpoint
{
    /// <summary>
    /// Gets the transport address of this endpoint (e.g. <c>rabbitmq://localhost/my-queue</c>).
    /// </summary>
    Uri Address { get; }

    /// <summary>
    /// Sends a typed message to the endpoint address.
    /// The message is serialized using the configured <c>IMessageSerializer</c>.
    /// </summary>
    /// <typeparam name="T">The message type. Must be a reference type (typically a <c>record</c>).</typeparam>
    /// <param name="message">The message to send. Must not be null.</param>
    /// <param name="cancellationToken">A token to cancel the send operation.</param>
    /// <returns>A <see cref="Task"/> that completes when the message has been accepted by the transport.</returns>
    Task SendAsync<T>(T message, CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>
    /// Sends a pre-serialized raw payload with an explicit content type to the endpoint address.
    /// Use this overload when the caller controls serialization, e.g. for forwarding or bridging scenarios.
    /// </summary>
    /// <param name="payload">The serialized message body. Must not be empty.</param>
    /// <param name="contentType">The MIME content type of the payload (e.g. <c>application/json</c>).</param>
    /// <param name="cancellationToken">A token to cancel the send operation.</param>
    /// <returns>A <see cref="Task"/> that completes when the message has been accepted by the transport.</returns>
    Task SendRawAsync(ReadOnlyMemory<byte> payload, string contentType,
        CancellationToken cancellationToken = default);
}
