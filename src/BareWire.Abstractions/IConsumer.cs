namespace BareWire.Abstractions;

/// <summary>
/// Defines a strongly-typed message consumer that processes messages of type <typeparamref name="T"/>.
/// Implement this interface to register a consumer with a receive endpoint.
/// Consumer instances are resolved from the DI container per-message and should be stateless or scoped.
/// </summary>
/// <typeparam name="T">
/// The message type this consumer handles. Must be a reference type (typically a <c>record</c>).
/// </typeparam>
public interface IConsumer<T> where T : class
{
    /// <summary>
    /// Processes a single message of type <typeparamref name="T"/>.
    /// Throwing an exception signals a processing failure; the transport will apply the configured
    /// retry and fault policy.
    /// </summary>
    /// <param name="context">
    /// The consume context providing access to the message, headers, and publish/send capabilities.
    /// </param>
    /// <returns>A <see cref="Task"/> that completes when message processing is finished.</returns>
    Task ConsumeAsync(ConsumeContext<T> context);
}
