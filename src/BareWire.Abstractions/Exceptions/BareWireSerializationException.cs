namespace BareWire.Abstractions.Exceptions;

/// <summary>
/// Thrown when BareWire cannot serialize or deserialize a message payload.
/// The raw payload, if available, is included for diagnostic purposes but is truncated to
/// <see cref="MaxRawPayloadLength"/> characters to prevent accidental secret exposure.
/// </summary>
public sealed class BareWireSerializationException : BareWireException
{
    /// <summary>
    /// The maximum number of characters preserved from the raw payload for diagnostics.
    /// Payloads longer than this value are truncated with a trailing indicator.
    /// </summary>
    public const int MaxRawPayloadLength = 500;

    /// <summary>
    /// Gets the MIME content type of the payload that failed serialization/deserialization
    /// (e.g. <c>"application/json"</c>).
    /// </summary>
    public string ContentType { get; }

    /// <summary>
    /// Gets the target CLR type that deserialization attempted to produce,
    /// or <see langword="null"/> for serialization failures where no target type applies.
    /// </summary>
    public Type? TargetType { get; }

    /// <summary>
    /// Gets up to <see cref="MaxRawPayloadLength"/> characters of the raw payload for diagnostics,
    /// or <see langword="null"/> when no payload is available.
    /// Long payloads are truncated with a <c>[truncated]</c> suffix.
    /// </summary>
    public string? RawPayload { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="BareWireSerializationException"/>.
    /// </summary>
    /// <param name="message">A human-readable description of the serialization error.</param>
    /// <param name="contentType">The MIME content type of the failing payload. Must not be null.</param>
    /// <param name="targetType">The CLR type targeted by deserialization, or <see langword="null"/>.</param>
    /// <param name="rawPayload">The raw payload string for diagnostics, or <see langword="null"/>. Truncated automatically.</param>
    /// <param name="innerException">An optional inner exception from the serializer.</param>
    public BareWireSerializationException(
        string message,
        string contentType,
        Type? targetType = null,
        string? rawPayload = null,
        Exception? innerException = null)
        : base(message, innerException!)
    {
        ContentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
        TargetType = targetType;
        RawPayload = Truncate(rawPayload);
    }

    private static string? Truncate(string? payload)
    {
        if (payload is null || payload.Length <= MaxRawPayloadLength)
        {
            return payload;
        }

        return string.Concat(payload.AsSpan(0, MaxRawPayloadLength), "[truncated]");
    }
}
