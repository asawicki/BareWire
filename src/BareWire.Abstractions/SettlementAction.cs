namespace BareWire.Abstractions;

/// <summary>
/// Defines the action to take when settling a consumed message with the broker.
/// </summary>
public enum SettlementAction
{
    /// <summary>
    /// Acknowledge the message — remove it from the queue permanently.
    /// </summary>
    Ack,

    /// <summary>
    /// Negatively acknowledge the message — return it to the queue for redelivery (broker-specific behaviour).
    /// </summary>
    Nack,

    /// <summary>
    /// Reject the message — discard it or route it to a dead-letter exchange, without requeuing.
    /// </summary>
    Reject,

    /// <summary>
    /// Defer processing — the message will be re-delivered after a delay determined by the transport.
    /// </summary>
    Defer,

    /// <summary>
    /// Requeue the message — return it to the head of the queue for immediate redelivery.
    /// </summary>
    Requeue,
}
