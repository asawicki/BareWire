namespace BareWire.Abstractions;

/// <summary>
/// Specifies the AMQP exchange type used when declaring or binding an exchange.
/// </summary>
public enum ExchangeType
{
    /// <summary>
    /// Routes messages to queues whose binding key exactly matches the routing key.
    /// </summary>
    Direct,

    /// <summary>
    /// Routes messages to all bound queues regardless of routing key.
    /// </summary>
    Fanout,

    /// <summary>
    /// Routes messages to queues based on wildcard matching of the routing key against binding patterns.
    /// </summary>
    Topic,

    /// <summary>
    /// Routes messages based on header attributes rather than routing keys.
    /// </summary>
    Headers,
}
