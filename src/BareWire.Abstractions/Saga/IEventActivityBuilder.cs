using System.Diagnostics.CodeAnalysis;

namespace BareWire.Abstractions.Saga;

/// <summary>
/// Provides a fluent API for defining the activities to perform when a SAGA receives an event
/// while in a particular state.
/// </summary>
/// <typeparam name="TSaga">The SAGA state type.</typeparam>
/// <typeparam name="TEvent">The event message type that triggers these activities.</typeparam>
public interface IEventActivityBuilder<TSaga, TEvent>
    where TSaga : class, ISagaState
    where TEvent : class
{
    /// <summary>
    /// Transitions the SAGA to the specified named state after executing this activity chain.
    /// </summary>
    /// <param name="state">The name of the target state.</param>
    /// <returns>The same builder instance for method chaining.</returns>
    IEventActivityBuilder<TSaga, TEvent> TransitionTo(string state);

    /// <summary>
    /// Executes an arbitrary asynchronous action with access to the current SAGA state and the incoming event.
    /// </summary>
    /// <param name="action">An asynchronous delegate that receives the SAGA instance and the event message.</param>
    /// <returns>The same builder instance for method chaining.</returns>
    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords",
        Justification = "Intentional DSL method name matching MassTransit-compatible API surface.")]
    IEventActivityBuilder<TSaga, TEvent> Then(Func<TSaga, TEvent, Task> action);

    /// <summary>
    /// Publishes a message produced from the current SAGA state and the incoming event.
    /// </summary>
    /// <typeparam name="T">The type of the message to publish.</typeparam>
    /// <param name="factory">A delegate that creates the message to publish.</param>
    /// <returns>The same builder instance for method chaining.</returns>
    IEventActivityBuilder<TSaga, TEvent> Publish<T>(Func<TSaga, TEvent, T> factory)
        where T : class;

    /// <summary>
    /// Sends a message to a specific endpoint produced from the current SAGA state and the incoming event.
    /// </summary>
    /// <typeparam name="T">The type of the message to send.</typeparam>
    /// <param name="destination">The URI of the destination endpoint.</param>
    /// <param name="factory">A delegate that creates the message to send.</param>
    /// <returns>The same builder instance for method chaining.</returns>
    IEventActivityBuilder<TSaga, TEvent> Send<T>(Uri destination, Func<TSaga, TEvent, T> factory)
        where T : class;

    /// <summary>
    /// Schedules a timeout message to be delivered after the configured delay.
    /// </summary>
    /// <typeparam name="T">The type of the timeout message.</typeparam>
    /// <param name="factory">A delegate that creates the timeout message.</param>
    /// <returns>The same builder instance for method chaining.</returns>
    IEventActivityBuilder<TSaga, TEvent> ScheduleTimeout<T>(Func<TSaga, TEvent, T> factory)
        where T : class;

    /// <summary>
    /// Schedules a timeout message using the delay and strategy from the given <see cref="ScheduleHandle{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the timeout message.</typeparam>
    /// <param name="factory">A delegate that creates the timeout message.</param>
    /// <param name="schedule">The schedule handle returned by <see cref="BareWireStateMachine{TSaga}.Schedule{T}"/>.</param>
    /// <returns>The same builder instance for method chaining.</returns>
    IEventActivityBuilder<TSaga, TEvent> ScheduleTimeout<T>(Func<TSaga, TEvent, T> factory, ScheduleHandle<T> schedule)
        where T : class;

    /// <summary>
    /// Cancels a previously scheduled timeout message of the given type.
    /// </summary>
    /// <typeparam name="T">The type of the timeout message to cancel.</typeparam>
    /// <returns>The same builder instance for method chaining.</returns>
    IEventActivityBuilder<TSaga, TEvent> CancelTimeout<T>()
        where T : class;

    /// <summary>
    /// Marks the SAGA instance as finalized, which causes the repository to delete it after the
    /// activity chain completes.
    /// </summary>
    /// <returns>The same builder instance for method chaining.</returns>
    IEventActivityBuilder<TSaga, TEvent> Finalize();
}
