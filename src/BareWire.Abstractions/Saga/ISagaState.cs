namespace BareWire.Abstractions.Saga;

/// <summary>
/// Represents the persisted state of a SAGA instance.
/// </summary>
/// <remarks>
/// All SAGA state classes must implement this interface. The state is persisted by an
/// <see cref="ISagaRepository{TSaga}"/> and correlated to incoming messages via <see cref="CorrelationId"/>.
/// </remarks>
public interface ISagaState
{
    /// <summary>
    /// Gets or sets the unique identifier that correlates all messages belonging to this SAGA instance.
    /// </summary>
    Guid CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the name of the state the SAGA is currently in (e.g. "Initial", "Submitted", "Completed").
    /// </summary>
    string CurrentState { get; set; }

    /// <summary>
    /// Gets or sets the optimistic concurrency version. Incremented on each update to detect concurrent modifications.
    /// </summary>
    int Version { get; set; }
}
