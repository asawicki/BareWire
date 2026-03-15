using BareWire.Abstractions.Pipeline;
using BareWire.Abstractions.Serialization;

namespace BareWire.Abstractions.Configuration;

/// <summary>
/// Provides the top-level fluent API for configuring a BareWire bus instance.
/// Passed to the configuration delegate registered with the DI container during application startup.
/// </summary>
public interface IBusConfigurator
{
    /// <summary>
    /// Configures the bus to use RabbitMQ as its transport layer.
    /// The <paramref name="configure"/> delegate receives a transport-specific configurator;
    /// the actual type is defined in <c>BareWire.Transport.RabbitMQ</c> and passed at runtime.
    /// </summary>
    /// <param name="configure">
    /// A delegate that receives the RabbitMQ configurator object and applies transport settings.
    /// The parameter type is <see cref="object"/> to avoid a compile-time dependency on the
    /// transport package from within <c>BareWire.Abstractions</c>.
    /// </param>
    void UseRabbitMQ(Action<object> configure);

    /// <summary>
    /// Configures observability (tracing, metrics, logging) for the bus.
    /// The <paramref name="configure"/> delegate receives an observability-specific configurator;
    /// the actual type is defined in <c>BareWire.Observability</c> and passed at runtime.
    /// </summary>
    /// <param name="configure">
    /// A delegate that receives the observability configurator object and applies settings.
    /// The parameter type is <see cref="object"/> to avoid a compile-time dependency on the
    /// observability package from within <c>BareWire.Abstractions</c>.
    /// </param>
    void ConfigureObservability(Action<object> configure);

    /// <summary>
    /// Adds a middleware component of type <typeparamref name="T"/> to the inbound message pipeline.
    /// Middleware is invoked in registration order for every message received by any endpoint on this bus.
    /// <typeparamref name="T"/> must be registered in the DI container.
    /// </summary>
    /// <typeparam name="T">
    /// The middleware implementation type. Must implement <see cref="IMessageMiddleware"/>.
    /// </typeparam>
    void AddMiddleware<T>() where T : class, IMessageMiddleware;

    /// <summary>
    /// Sets the primary message serializer for the bus.
    /// The serializer is used for all outbound typed message publishing unless overridden per-endpoint.
    /// <typeparamref name="T"/> must be registered in the DI container.
    /// </summary>
    /// <typeparam name="T">
    /// The serializer implementation type. Must implement <see cref="IMessageSerializer"/>.
    /// </typeparam>
    void UseSerializer<T>() where T : class, IMessageSerializer;
}
