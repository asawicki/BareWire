namespace BareWire.Abstractions;

/// <summary>
/// Provides publish capabilities for fan-out message distribution to all subscribed consumers.
/// Implementations are thread-safe and may be called concurrently.
/// </summary>
public interface IPublishEndpoint
{
    /// <summary>
    /// Publishes a typed message to all consumers subscribed to <typeparamref name="T"/>.
    /// The message is serialized using the configured <c>IMessageSerializer</c>.
    /// </summary>
    /// <typeparam name="T">The message type. Must be a reference type (typically a <c>record</c>).</typeparam>
    /// <param name="message">The message to publish. Must not be null.</param>
    /// <param name="cancellationToken">A token to cancel the publish operation.</param>
    /// <returns>A <see cref="Task"/> that completes when the message has been accepted by the transport.</returns>
    Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>
    /// Publishes a pre-serialized raw payload with an explicit content type to all subscribers.
    /// Use this overload when the caller controls serialization, e.g. for forwarding or bridging scenarios.
    /// </summary>
    /// <param name="payload">The serialized message body. Must not be empty.</param>
    /// <param name="contentType">The MIME content type of the payload (e.g. <c>application/json</c>).</param>
    /// <param name="cancellationToken">A token to cancel the publish operation.</param>
    /// <returns>A <see cref="Task"/> that completes when the message has been accepted by the transport.</returns>
    Task PublishRawAsync(ReadOnlyMemory<byte> payload, string contentType,
        CancellationToken cancellationToken = default);
}
