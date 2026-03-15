namespace BareWire.Abstractions.Exceptions;

/// <summary>
/// Represents a saga-level error in BareWire, such as a failure to load, persist,
/// or transition a saga state machine instance.
/// </summary>
/// <remarks>
/// This class is not sealed because <see cref="ConcurrencyException"/> derives from it.
/// Catch the most-derived type for targeted handling.
/// </remarks>
public class BareWireSagaException : BareWireException
{
    /// <summary>
    /// Gets the CLR type of the saga state machine that raised the error.
    /// </summary>
    public Type SagaType { get; }

    /// <summary>
    /// Gets the correlation identifier of the saga instance involved in the failure.
    /// </summary>
    public Guid CorrelationId { get; }

    /// <summary>
    /// Gets the name of the state the saga was in when the error occurred,
    /// or <see langword="null"/> when the state is unknown (e.g. during initial load).
    /// </summary>
    public string? CurrentState { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="BareWireSagaException"/>.
    /// </summary>
    /// <param name="message">A human-readable description of the saga error.</param>
    /// <param name="sagaType">The CLR type of the affected saga. Must not be null.</param>
    /// <param name="correlationId">The correlation identifier of the affected saga instance.</param>
    /// <param name="currentState">The state the saga was in, or <see langword="null"/> if unknown.</param>
    /// <param name="innerException">An optional inner exception from the saga infrastructure.</param>
    public BareWireSagaException(
        string message,
        Type sagaType,
        Guid correlationId,
        string? currentState = null,
        Exception? innerException = null)
        : base(message, innerException!)
    {
        SagaType = sagaType ?? throw new ArgumentNullException(nameof(sagaType));
        CorrelationId = correlationId;
        CurrentState = currentState;
    }
}
