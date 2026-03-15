namespace BareWire.Abstractions.Exceptions;

/// <summary>
/// Thrown when a request/response operation via <c>IRequestClient&lt;TRequest&gt;</c> does not
/// receive a response within the configured timeout period.
/// </summary>
public sealed class RequestTimeoutException : BareWireTransportException
{
    /// <summary>
    /// Gets the CLR type of the request message that timed out.
    /// </summary>
    public Type RequestType { get; }

    /// <summary>
    /// Gets the duration the client waited before the operation was cancelled.
    /// </summary>
    public TimeSpan Timeout { get; }

    /// <summary>
    /// Gets the transport address of the endpoint to which the request was sent,
    /// or <see langword="null"/> when no destination address is available.
    /// </summary>
    public Uri? DestinationAddress { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="RequestTimeoutException"/>.
    /// </summary>
    /// <param name="requestType">The CLR type of the timed-out request message. Must not be null.</param>
    /// <param name="timeout">The duration the client waited before timing out.</param>
    /// <param name="destinationAddress">The endpoint URI the request was sent to, or <see langword="null"/>.</param>
    /// <param name="transportName">The logical name of the transport (e.g. <c>"RabbitMQ"</c>).</param>
    /// <param name="innerException">An optional inner exception from the timeout mechanism.</param>
    public RequestTimeoutException(
        Type requestType,
        TimeSpan timeout,
        Uri? destinationAddress,
        string transportName = "Unknown",
        Exception? innerException = null)
        : base(
            BuildMessage(requestType, timeout, destinationAddress),
            transportName,
            destinationAddress,
            innerException)
    {
        RequestType = requestType ?? throw new ArgumentNullException(nameof(requestType));
        Timeout = timeout;
        DestinationAddress = destinationAddress;
    }

    private static string BuildMessage(Type requestType, TimeSpan timeout, Uri? destinationAddress)
    {
        var destination = destinationAddress is not null ? $" Destination: {destinationAddress}." : string.Empty;
        return $"Request of type '{requestType.Name}' timed out after {timeout.TotalSeconds:0.###}s.{destination}";
    }
}
