namespace BareWire.Abstractions.Configuration;

/// <summary>
/// Captures a consumer type and the message type it handles, bridging the gap between
/// transport-level endpoint configuration (e.g. <c>e.Consumer&lt;TConsumer, TMessage&gt;()</c>)
/// and the core consume loop that needs both types to deserialize and dispatch.
/// </summary>
public sealed record ConsumerRegistration(Type ConsumerType, Type MessageType);
