namespace BareWire.Abstractions.Exceptions;

/// <summary>
/// Thrown when BareWire fails to declare or bind a topology element (exchange, queue, or binding)
/// against the broker during <c>IBusControl.DeployTopologyAsync</c>.
/// </summary>
public sealed class TopologyDeploymentException : BareWireTransportException
{
    /// <summary>
    /// Gets the name of the topology element that could not be deployed
    /// (e.g. the exchange name, queue name, or binding key).
    /// </summary>
    public string TopologyElement { get; }

    /// <summary>
    /// Gets the raw broker error message returned by the transport,
    /// or <see langword="null"/> when no broker-level detail is available.
    /// </summary>
    public string? BrokerError { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="TopologyDeploymentException"/>.
    /// </summary>
    /// <param name="topologyElement">The name of the element that failed to deploy. Must not be null.</param>
    /// <param name="transportName">The logical name of the transport (e.g. <c>"RabbitMQ"</c>).</param>
    /// <param name="brokerError">The raw error from the broker, or <see langword="null"/>.</param>
    /// <param name="endpointAddress">The broker endpoint URI, or <see langword="null"/>.</param>
    /// <param name="innerException">An optional inner exception from the transport layer.</param>
    public TopologyDeploymentException(
        string topologyElement,
        string transportName = "Unknown",
        string? brokerError = null,
        Uri? endpointAddress = null,
        Exception? innerException = null)
        : base(
            BuildMessage(topologyElement, brokerError),
            transportName,
            endpointAddress,
            innerException)
    {
        TopologyElement = topologyElement ?? throw new ArgumentNullException(nameof(topologyElement));
        BrokerError = brokerError;
    }

    private static string BuildMessage(string topologyElement, string? brokerError)
    {
        var detail = brokerError is not null ? $" Broker error: {brokerError}." : string.Empty;
        return $"Failed to deploy topology element '{topologyElement}'.{detail}";
    }
}
