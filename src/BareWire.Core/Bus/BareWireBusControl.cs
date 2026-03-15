using BareWire.Abstractions;
using BareWire.Abstractions.Configuration;
using BareWire.Abstractions.Topology;
using BareWire.Abstractions.Transport;
using BareWire.Core.Configuration;
using BareWire.Core.FlowControl;
using Microsoft.Extensions.Logging;

namespace BareWire.Core.Bus;

internal sealed partial class BareWireBusControl : IBusControl
{
    private readonly BareWireBus _bus;
    private readonly ITransportAdapter _adapter;
    private readonly FlowController _flowController;
    private readonly BusConfigurator _configurator;
    private readonly ILogger<BareWireBusControl> _logger;

    private readonly object _stateLock = new();
    private bool _started;

    internal BareWireBusControl(
        BareWireBus bus,
        ITransportAdapter adapter,
        FlowController flowController,
        BusConfigurator configurator,
        ILogger<BareWireBusControl> logger)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _flowController = flowController ?? throw new ArgumentNullException(nameof(flowController));
        _configurator = configurator ?? throw new ArgumentNullException(nameof(configurator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ── IBusControl ───────────────────────────────────────────────────────────

    public async Task<BusHandle> StartAsync(CancellationToken cancellationToken = default)
    {
        // Fail fast: validate configuration before attempting to start the bus.
        ConfigurationValidator.Validate(_configurator);

        lock (_stateLock)
        {
            if (_started)
                throw new InvalidOperationException("Bus is already started. Call StopAsync before starting again.");

            _started = true;
        }

        LogBusStarting(_logger, _bus.BusId);

        _bus.StartPublishing();

        LogBusStarted(_logger, _bus.BusId);

        return await Task.FromResult(new BusHandle(_bus.BusId)).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            if (!_started)
                return;

            _started = false;
        }

        LogBusStopping(_logger, _bus.BusId);

        await _bus.DisposeAsync().ConfigureAwait(false);

        LogBusStopped(_logger, _bus.BusId);
    }

    public async Task DeployTopologyAsync(CancellationToken cancellationToken = default)
    {
        // Deploy an empty topology by default. Full topology wiring is added in Phase 3 (RabbitMQ transport).
        TopologyDeclaration emptyTopology = new();
        await _adapter.DeployTopologyAsync(emptyTopology, cancellationToken).ConfigureAwait(false);
    }

    public BusHealthStatus CheckHealth()
    {
        IReadOnlyCollection<string> endpointNames = _flowController.GetAllEndpointNames();

        List<EndpointHealthStatus> endpointStatuses = new(endpointNames.Count);
        BusStatus worstStatus = BusStatus.Healthy;

        foreach (string endpointName in endpointNames)
        {
            BusStatus status = _flowController.CheckHealth(endpointName);

            if (status > worstStatus)
                worstStatus = status;

            endpointStatuses.Add(new EndpointHealthStatus(endpointName, status, Description: null));
        }

        string description = worstStatus switch
        {
            BusStatus.Healthy => "All endpoints are operating normally.",
            BusStatus.Degraded => "One or more endpoints are approaching capacity.",
            BusStatus.Unhealthy => "One or more endpoints are at capacity.",
            _ => "Unknown status.",
        };

        return new BusHealthStatus(worstStatus, description, endpointStatuses);
    }

    // ── IBus delegation ───────────────────────────────────────────────────────

    public Guid BusId => _bus.BusId;
    public Uri Address => _bus.Address;

    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
        => _bus.PublishAsync(message, cancellationToken);

    public Task PublishRawAsync(ReadOnlyMemory<byte> payload, string contentType, CancellationToken cancellationToken = default)
        => _bus.PublishRawAsync(payload, contentType, cancellationToken);

    public Task<ISendEndpoint> GetSendEndpoint(Uri address, CancellationToken cancellationToken = default)
        => _bus.GetSendEndpoint(address, cancellationToken);

    public IRequestClient<T> CreateRequestClient<T>() where T : class
        => _bus.CreateRequestClient<T>();

    public IDisposable ConnectReceiveEndpoint(string queueName, Action<IReceiveEndpointConfigurator> configure)
        => _bus.ConnectReceiveEndpoint(queueName, configure);

    // ── IAsyncDisposable / IDisposable ────────────────────────────────────────

    public ValueTask DisposeAsync() => _bus.DisposeAsync();
    public void Dispose() => _bus.Dispose();

    // ── Logger messages ───────────────────────────────────────────────────────

    [LoggerMessage(Level = LogLevel.Information, Message = "BareWire bus {BusId} starting.")]
    private static partial void LogBusStarting(ILogger logger, Guid busId);

    [LoggerMessage(Level = LogLevel.Information, Message = "BareWire bus {BusId} started.")]
    private static partial void LogBusStarted(ILogger logger, Guid busId);

    [LoggerMessage(Level = LogLevel.Information, Message = "BareWire bus {BusId} stopping.")]
    private static partial void LogBusStopping(ILogger logger, Guid busId);

    [LoggerMessage(Level = LogLevel.Information, Message = "BareWire bus {BusId} stopped.")]
    private static partial void LogBusStopped(ILogger logger, Guid busId);
}
