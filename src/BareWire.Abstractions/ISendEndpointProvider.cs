namespace BareWire.Abstractions;

/// <summary>
/// Resolves <see cref="ISendEndpoint"/> instances by transport address.
/// Implementations may cache endpoints and are expected to be thread-safe.
/// </summary>
public interface ISendEndpointProvider
{
    /// <summary>
    /// Returns the <see cref="ISendEndpoint"/> for the specified transport <paramref name="address"/>.
    /// Implementations may create or retrieve a cached endpoint.
    /// </summary>
    /// <param name="address">
    /// The fully-qualified transport address of the target endpoint
    /// (e.g. <c>rabbitmq://localhost/my-queue</c>).
    /// </param>
    /// <param name="cancellationToken">A token to cancel the resolution operation.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> that resolves to the <see cref="ISendEndpoint"/>
    /// for the given address.
    /// </returns>
    Task<ISendEndpoint> GetSendEndpoint(Uri address, CancellationToken cancellationToken = default);
}
