namespace BareWire.Abstractions;

/// <summary>
/// Controls the behaviour of the raw (envelope-free) serializer when processing messages.
/// Multiple flags can be combined using bitwise OR.
/// </summary>
[Flags]
public enum RawSerializerOptions
{
    /// <summary>
    /// No special serializer options — raw payload is written as-is with no additional headers or type metadata.
    /// </summary>
    None = 0,

    /// <summary>
    /// Instructs the serializer to add standard BareWire transport headers (e.g. content-type, message-id) to the outgoing message.
    /// </summary>
    AddTransportHeaders = 1,

    /// <summary>
    /// Instructs the serializer to copy all inbound headers verbatim into the outbound message during forwarding or re-publish.
    /// </summary>
    CopyHeaders = 2,

    /// <summary>
    /// Allows the raw serializer to accept and emit any message type without a compile-time type constraint check.
    /// </summary>
    AnyMessageType = 4,

    /// <summary>
    /// Default raw serializer behaviour: adds transport headers and allows any message type.
    /// Equivalent to <see cref="AddTransportHeaders"/> | <see cref="AnyMessageType"/>.
    /// </summary>
    Default = AddTransportHeaders | AnyMessageType,
}
