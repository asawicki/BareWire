using System.Collections.Concurrent;
using BareWire.Abstractions;
using Microsoft.Extensions.Logging;

namespace BareWire.FlowControl;

internal sealed partial class FlowController
{
    private readonly ILogger<FlowController> _logger;
    private readonly ConcurrentDictionary<string, CreditManager> _managers = new();

    internal FlowController(ILogger<FlowController> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    internal CreditManager GetOrCreateManager(string endpointName, FlowControlOptions options)
    {
        ArgumentNullException.ThrowIfNull(endpointName);
        ArgumentNullException.ThrowIfNull(options);

        return _managers.GetOrAdd(endpointName, _ => new CreditManager(options));
    }

    internal BusStatus CheckHealth(string endpointName)
    {
        ArgumentNullException.ThrowIfNull(endpointName);

        if (!_managers.TryGetValue(endpointName, out CreditManager? manager))
            return BusStatus.Healthy;

        if (manager.IsAtCapacity)
            return BusStatus.Unhealthy;

        double utilization = manager.UtilizationPercent;

        if (utilization >= 90.0)
        {
            LogHealthAlert(_logger, endpointName, utilization);
            return BusStatus.Degraded;
        }

        return BusStatus.Healthy;
    }

    internal IReadOnlyCollection<string> GetAllEndpointNames() => _managers.Keys.ToArray();

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Flow control health alert: endpoint '{EndpointName}' is at {Utilization:F1}% capacity.")]
    private static partial void LogHealthAlert(ILogger logger, string endpointName, double utilization);
}
