namespace BareWire.Abstractions.Saga;

/// <summary>
/// Represents a compensable activity within a SAGA — an operation that can be undone if a subsequent
/// step in the SAGA fails.
/// </summary>
/// <typeparam name="TArguments">
/// The type carrying the input data required to execute the activity. Must be a reference type.
/// </typeparam>
/// <typeparam name="TLog">
/// The type carrying the data recorded during execution that is needed to perform compensation.
/// Must be a reference type.
/// </typeparam>
public interface ICompensableActivity<TArguments, TLog>
    where TArguments : class
    where TLog : class
{
    /// <summary>
    /// Executes the activity and returns a log entry that captures the state needed for compensation.
    /// </summary>
    /// <param name="arguments">The input data for the activity.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A <typeparamref name="TLog"/> instance that records what was done, used later by
    /// <see cref="CompensateAsync"/> to undo the operation.
    /// </returns>
    Task<TLog> ExecuteAsync(TArguments arguments, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reverses the effects of a previously executed activity using the recorded log entry.
    /// </summary>
    /// <param name="log">The log entry produced by a prior call to <see cref="ExecuteAsync"/>.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task CompensateAsync(TLog log, CancellationToken cancellationToken = default);
}
