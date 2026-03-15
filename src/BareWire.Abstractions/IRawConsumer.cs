namespace BareWire.Abstractions;

/// <summary>
/// Defines a raw message consumer that receives the undeserialized byte payload directly from the transport.
/// Implement this interface when full control over deserialization is required, e.g. for routing slips,
/// protocol bridges, or custom binary formats.
/// Consumer instances are resolved from the DI container per-message.
/// </summary>
public interface IRawConsumer
{
    /// <summary>
    /// Processes a single raw (undeserialized) message.
    /// Throwing an exception signals a processing failure; the transport will apply the configured
    /// retry and fault policy.
    /// </summary>
    /// <param name="context">
    /// The raw consume context providing access to the byte body, headers, and publish/send capabilities.
    /// </param>
    /// <returns>A <see cref="Task"/> that completes when message processing is finished.</returns>
    Task ConsumeAsync(RawConsumeContext context);
}
