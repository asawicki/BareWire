namespace BareWire.Abstractions;

/// <summary>
/// Represents an active, running bus instance returned by <see cref="IBusControl.StartAsync"/>.
/// Disposing this handle signals intent to stop the bus; call <see cref="IBusControl.StopAsync"/>
/// before disposing for a graceful shutdown.
/// </summary>
public sealed class BusHandle : IAsyncDisposable
{
    /// <summary>
    /// Gets the unique identifier of the running bus instance.
    /// Matches the <see cref="IBus.BusId"/> of the bus that produced this handle.
    /// </summary>
    public Guid BusId { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="BusHandle"/> with the given bus identifier.
    /// </summary>
    /// <param name="busId">The unique identifier of the running bus.</param>
    public BusHandle(Guid busId)
    {
        BusId = busId;
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting
    /// unmanaged resources asynchronously.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> that completes when all resources have been released.</returns>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
