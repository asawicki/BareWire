using BareWire.Abstractions.Saga;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace BareWire.Saga;

/// <summary>
/// Provides extension methods on <see cref="IServiceCollection"/> for registering BareWire
/// saga state machines with the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a saga state machine and wires its message dispatcher into the BareWire
    /// consume pipeline. Call this alongside <c>AddBareWireSaga&lt;TSaga&gt;()</c> which
    /// registers the repository.
    /// </summary>
    /// <typeparam name="TStateMachine">
    /// The state machine type — the class derived from
    /// <see cref="BareWireStateMachine{TSaga}"/>.
    /// </typeparam>
    /// <typeparam name="TSaga">
    /// The saga state type managed by <typeparamref name="TStateMachine"/>.
    /// Must implement <see cref="ISagaState"/> and have a public parameterless constructor.
    /// </typeparam>
    /// <param name="services">The service collection to register services into.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddBareWireSagaStateMachine<TStateMachine, TSaga>(
        this IServiceCollection services)
        where TStateMachine : BareWireStateMachine<TSaga>
        where TSaga : class, ISagaState, new()
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register the state machine itself as a singleton if not already registered.
        services.TryAddSingleton<TStateMachine>();

        // Register the StateMachineDefinition built from the singleton state machine.
        services.TryAddSingleton(sp =>
        {
            TStateMachine machine = sp.GetRequiredService<TStateMachine>();
            return StateMachineDefinition<TSaga>.Build(machine);
        });

        // NOTE: StateMachineExecutor<TSaga> is NOT registered as singleton because it depends
        // on ISagaRepository<TSaga> which is scoped (EF Core DbContext). Instead, the dispatcher
        // creates executors per-message using IServiceScopeFactory.

        // Register the ISagaMessageDispatcher implementation for this state machine.
        // Multiple saga machines register multiple ISagaMessageDispatcher entries.
        // The dispatcher uses IServiceScopeFactory to resolve ISagaRepository per-message,
        // avoiding the captive dependency problem (scoped repo in singleton executor).
        services.AddSingleton<ISagaMessageDispatcher>(sp => new SagaMessageDispatcher<TStateMachine, TSaga>(
            sp.GetRequiredService<StateMachineDefinition<TSaga>>(),
            sp.GetRequiredService<TStateMachine>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ILoggerFactory>()));

        return services;
    }
}
