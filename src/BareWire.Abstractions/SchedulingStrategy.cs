namespace BareWire.Abstractions;

/// <summary>
/// Determines the strategy used for scheduling delayed or deferred messages in a SAGA or retry scenario.
/// </summary>
public enum SchedulingStrategy
{
    /// <summary>
    /// BareWire automatically selects the most appropriate strategy based on available transport capabilities.
    /// </summary>
    Auto,

    /// <summary>
    /// Uses the transport's native scheduling feature (e.g. RabbitMQ delayed message plugin).
    /// </summary>
    TransportNative,

    /// <summary>
    /// Implements scheduling by requeueing the message with a calculated delay using message TTL or dead-lettering.
    /// </summary>
    DelayRequeue,

    /// <summary>
    /// Delegates scheduling to an external scheduler service (e.g. Quartz.NET, Hangfire).
    /// </summary>
    ExternalScheduler,

    /// <summary>
    /// Uses a dedicated delay exchange/topic pattern to achieve per-message delay granularity.
    /// </summary>
    DelayTopic,
}
