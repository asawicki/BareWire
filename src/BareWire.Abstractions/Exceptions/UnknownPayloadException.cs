namespace BareWire.Abstractions.Exceptions;

/// <summary>
/// Thrown when a receive endpoint receives a message with a content type or message type header
/// that no registered consumer or deserializer can handle.
/// </summary>
public sealed class UnknownPayloadException : BareWireException
{
    /// <summary>
    /// Gets the MIME content type from the inbound message that could not be matched to a deserializer,
    /// or <see langword="null"/> when no content-type header was present.
    /// </summary>
    public string? ContentType { get; }

    /// <summary>
    /// Gets the name of the receive endpoint that received the unrecognised payload.
    /// </summary>
    public string EndpointName { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="UnknownPayloadException"/>.
    /// </summary>
    /// <param name="endpointName">The name of the receive endpoint. Must not be null.</param>
    /// <param name="contentType">The unrecognised content type, or <see langword="null"/> if absent from headers.</param>
    /// <param name="innerException">An optional inner exception.</param>
    public UnknownPayloadException(
        string endpointName,
        string? contentType = null,
        Exception? innerException = null)
        : base(BuildMessage(endpointName, contentType), innerException!)
    {
        EndpointName = endpointName ?? throw new ArgumentNullException(nameof(endpointName));
        ContentType = contentType;
    }

    private static string BuildMessage(string endpointName, string? contentType)
    {
        var ct = contentType is not null ? $" Content-Type: '{contentType}'." : " No Content-Type header present.";
        return $"Endpoint '{endpointName}' received a payload with no matching consumer or deserializer.{ct}";
    }
}
