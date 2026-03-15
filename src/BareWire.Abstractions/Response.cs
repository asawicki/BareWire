namespace BareWire.Abstractions;

/// <summary>
/// Wraps a response message received via the request/response pattern, providing access to the
/// deserialized payload alongside its transport-level metadata.
/// </summary>
/// <typeparam name="T">
/// The response message type. Must be a reference type (typically a <c>record</c>).
/// </typeparam>
/// <param name="MessageId">The unique identifier of the response message assigned by the transport.</param>
/// <param name="Headers">
/// The transport-level headers attached to the response message.
/// Never null — an empty dictionary is returned when no headers are present.
/// </param>
/// <param name="Message">The deserialized response message payload.</param>
public sealed record Response<T>(
    Guid MessageId,
    IReadOnlyDictionary<string, string> Headers,
    T Message)
    where T : class;
