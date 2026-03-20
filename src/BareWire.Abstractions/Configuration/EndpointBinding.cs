namespace BareWire.Abstractions.Configuration;

/// <summary>
/// Transport-agnostic description of a receive endpoint: queue name, prefetch settings,
/// and the consumer registrations that should be dispatched for inbound messages.
/// Registered in DI by the transport package and consumed by the core bus startup.
/// </summary>
public sealed class EndpointBinding
{
    /// <summary>Gets the transport queue / endpoint name to consume from.</summary>
    public required string EndpointName { get; init; }

    /// <summary>Gets the prefetch count that maps to <see cref="FlowControlOptions.MaxInFlightMessages"/>.</summary>
    public int PrefetchCount { get; init; } = 16;

    /// <summary>Gets the consumer registrations for this endpoint.</summary>
    public IReadOnlyList<ConsumerRegistration> Consumers { get; init; } = [];

    /// <summary>Gets the raw consumer types registered on this endpoint.</summary>
    public IReadOnlyList<Type> RawConsumers { get; init; } = [];

    /// <summary>Gets the saga state machine types registered on this endpoint.</summary>
    public IReadOnlyList<Type> SagaTypes { get; init; } = [];
}
