namespace BareWire.Abstractions.Exceptions;

/// <summary>
/// Thrown when an optimistic concurrency conflict is detected while persisting a saga instance.
/// This occurs when two concurrent operations attempt to update the same saga at the same version,
/// and the underlying repository rejects the write to preserve consistency.
/// </summary>
public sealed class ConcurrencyException : BareWireSagaException
{
    /// <summary>
    /// Gets the saga version that the current operation expected to find in the repository.
    /// </summary>
    public int ExpectedVersion { get; }

    /// <summary>
    /// Gets the saga version actually found in the repository at the time of the conflict.
    /// </summary>
    public int ActualVersion { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ConcurrencyException"/>.
    /// </summary>
    /// <param name="sagaType">The CLR type of the saga with the conflicting update. Must not be null.</param>
    /// <param name="correlationId">The correlation identifier of the affected saga instance.</param>
    /// <param name="expectedVersion">The version the operation expected to write against.</param>
    /// <param name="actualVersion">The version found in the repository, indicating a concurrent modification.</param>
    /// <param name="currentState">The state the saga was in at the time of conflict, or <see langword="null"/>.</param>
    /// <param name="innerException">An optional inner exception from the repository layer.</param>
    public ConcurrencyException(
        Type sagaType,
        Guid correlationId,
        int expectedVersion,
        int actualVersion,
        string? currentState = null,
        Exception? innerException = null)
        : base(
            BuildMessage(sagaType, correlationId, expectedVersion, actualVersion),
            sagaType,
            correlationId,
            currentState,
            innerException)
    {
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }

    private static string BuildMessage(Type sagaType, Guid correlationId, int expectedVersion, int actualVersion)
        => $"Concurrency conflict on saga '{sagaType.Name}' (correlationId={correlationId}): " +
           $"expected version {expectedVersion}, but found {actualVersion}.";
}
