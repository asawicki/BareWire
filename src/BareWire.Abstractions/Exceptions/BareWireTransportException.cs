namespace BareWire.Abstractions.Exceptions;

/// <summary>
/// Represents a transport-level error in BareWire, such as a connection failure,
/// message delivery rejection, or protocol error.
/// </summary>
/// <remarks>
/// This class is not sealed because transport-specific exceptions
/// (<see cref="RequestTimeoutException"/>, <see cref="TopologyDeploymentException"/>)
/// derive from it. Catch the most-derived type for targeted handling.
/// </remarks>
public class BareWireTransportException : BareWireException
{
    /// <summary>
    /// Gets the logical name of the transport that raised the error (e.g. <c>"RabbitMQ"</c>).
    /// </summary>
    public string TransportName { get; }

    /// <summary>
    /// Gets the URI of the broker endpoint involved in the failure,
    /// or <see langword="null"/> when no endpoint address is applicable.
    /// </summary>
    public Uri? EndpointAddress { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="BareWireTransportException"/>.
    /// </summary>
    /// <param name="message">A human-readable description of the transport error.</param>
    /// <param name="transportName">The logical name of the transport (e.g. <c>"RabbitMQ"</c>). Must not be null.</param>
    /// <param name="endpointAddress">The endpoint URI involved in the failure, or <see langword="null"/>.</param>
    /// <param name="innerException">An optional inner exception from the transport layer.</param>
    public BareWireTransportException(
        string message,
        string transportName,
        Uri? endpointAddress,
        Exception? innerException = null)
        : base(message, innerException!)
    {
        TransportName = transportName ?? throw new ArgumentNullException(nameof(transportName));
        EndpointAddress = endpointAddress;
    }
}
