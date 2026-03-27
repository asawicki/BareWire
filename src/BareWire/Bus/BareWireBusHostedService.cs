using BareWire.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BareWire.Bus;

internal sealed partial class BareWireBusHostedService : IHostedService
{
    private readonly IBusControl _busControl;
    private readonly ILogger<BareWireBusHostedService> _logger;

    public BareWireBusHostedService(IBusControl busControl, ILogger<BareWireBusHostedService> logger)
    {
        _busControl = busControl ?? throw new ArgumentNullException(nameof(busControl));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        LogStarting(_logger);
        await _busControl.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        LogStopping(_logger);
        await _busControl.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "BareWire hosted service starting.")]
    private static partial void LogStarting(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "BareWire hosted service stopping.")]
    private static partial void LogStopping(ILogger logger);
}
