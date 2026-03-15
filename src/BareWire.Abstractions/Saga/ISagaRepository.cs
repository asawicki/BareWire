namespace BareWire.Abstractions.Saga;

/// <summary>
/// Provides storage and retrieval operations for SAGA state instances.
/// </summary>
/// <typeparam name="TSaga">The SAGA state type managed by this repository.</typeparam>
public interface ISagaRepository<TSaga>
    where TSaga : class, ISagaState
{
    /// <summary>
    /// Retrieves a SAGA instance by its correlation identifier, or <see langword="null"/> if not found.
    /// </summary>
    /// <param name="correlationId">The unique correlation identifier of the SAGA instance to retrieve.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// The SAGA state instance if found; otherwise <see langword="null"/>.
    /// </returns>
    Task<TSaga?> FindAsync(Guid correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a new SAGA instance to the underlying store.
    /// </summary>
    /// <param name="saga">The new SAGA state instance to save.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a SAGA with the same <see cref="ISagaState.CorrelationId"/> already exists.
    /// </exception>
    Task SaveAsync(TSaga saga, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing SAGA instance in the underlying store.
    /// </summary>
    /// <param name="saga">The SAGA state instance with updated values.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the stored <see cref="ISagaState.Version"/> does not match the expected version,
    /// indicating a concurrent modification detected by optimistic concurrency control.
    /// </exception>
    Task UpdateAsync(TSaga saga, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a SAGA instance from the underlying store.
    /// </summary>
    /// <param name="correlationId">The unique correlation identifier of the SAGA instance to delete.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task DeleteAsync(Guid correlationId, CancellationToken cancellationToken = default);
}
