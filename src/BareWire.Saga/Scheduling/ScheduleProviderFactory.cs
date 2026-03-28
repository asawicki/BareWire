using BareWire.Abstractions;
using BareWire.Abstractions.Serialization;
using BareWire.Abstractions.Transport;
using Microsoft.Extensions.Logging;

namespace BareWire.Saga.Scheduling;

internal static class ScheduleProviderFactory
{
    internal static IScheduleProvider Create(
        SchedulingStrategy strategy,
        ITransportAdapter transport,
        ILoggerFactory loggerFactory,
        IMessageSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(serializer);

        return strategy switch
        {
            SchedulingStrategy.Auto => ResolveAuto(transport, serializer, loggerFactory),
            SchedulingStrategy.DelayRequeue => new DelayRequeueScheduleProvider(
                transport, serializer, loggerFactory.CreateLogger<DelayRequeueScheduleProvider>()),
            SchedulingStrategy.TransportNative => throw new NotSupportedException(
                "Transport-native scheduling is not yet implemented."),
            SchedulingStrategy.DelayTopic => throw new NotSupportedException(
                "Delay-topic scheduling is not yet implemented."),
            SchedulingStrategy.ExternalScheduler => throw new NotSupportedException(
                "External scheduler is not yet implemented."),
            _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy,
                $"Unknown scheduling strategy: {strategy}"),
        };
    }

    private static DelayRequeueScheduleProvider ResolveAuto(
        ITransportAdapter transport,
        IMessageSerializer serializer,
        ILoggerFactory loggerFactory)
    {
        // For now, Auto always resolves to DelayRequeue — the only fully implemented strategy.
        // Future: inspect transport.TransportName to select the optimal strategy per transport
        // (e.g. TransportNative for RabbitMQ delayed message plugin, TransportNative for ASB).
        return new DelayRequeueScheduleProvider(
            transport, serializer, loggerFactory.CreateLogger<DelayRequeueScheduleProvider>());
    }
}
