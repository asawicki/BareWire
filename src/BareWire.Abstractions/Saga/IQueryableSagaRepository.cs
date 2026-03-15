using System.Linq.Expressions;

namespace BareWire.Abstractions.Saga;

/// <summary>
/// Extends <see cref="ISagaRepository{TSaga}"/> with predicate-based query capabilities.
/// </summary>
/// <typeparam name="TSaga">The SAGA state type managed by this repository.</typeparam>
/// <remarks>
/// Not all repository implementations support arbitrary predicate queries. Use this interface
/// only when the underlying store (e.g. Entity Framework, MongoDB) can translate
/// <see cref="Expression{TDelegate}"/> predicates efficiently.
/// </remarks>
public interface IQueryableSagaRepository<TSaga> : ISagaRepository<TSaga>
    where TSaga : class, ISagaState
{
    /// <summary>
    /// Retrieves a single SAGA instance matching the given predicate, or <see langword="null"/> if none matches.
    /// </summary>
    /// <param name="predicate">An expression tree used to filter SAGA instances in the underlying store.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// The matching SAGA state instance if exactly one match is found; otherwise <see langword="null"/>.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when more than one SAGA instance matches the predicate.
    /// </exception>
    Task<TSaga?> FindSingleAsync(
        Expression<Func<TSaga, bool>> predicate,
        CancellationToken cancellationToken = default);
}
