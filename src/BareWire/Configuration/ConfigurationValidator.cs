using BareWire.Abstractions.Exceptions;

namespace BareWire.Configuration;

/// <summary>
/// Provides fail-fast validation of a <see cref="BusConfigurator"/> before the bus starts.
/// All validation errors throw <see cref="BareWireConfigurationException"/> with a clear message
/// so that misconfiguration is caught at startup rather than at runtime.
/// </summary>
internal static class ConfigurationValidator
{
    /// <summary>
    /// Validates the supplied <paramref name="configurator"/> and throws
    /// <see cref="BareWireConfigurationException"/> on the first validation failure found.
    /// </summary>
    /// <param name="configurator">The bus configurator to validate.</param>
    /// <exception cref="BareWireConfigurationException">
    /// Thrown when any required configuration is missing or invalid.
    /// </exception>
    internal static void Validate(BusConfigurator configurator)
    {
        ArgumentNullException.ThrowIfNull(configurator);

        ValidateTransport(configurator);
        ValidateReceiveEndpoints(configurator);
    }

    private static void ValidateTransport(BusConfigurator configurator)
    {
        if (!configurator.HasTransport)
        {
            throw new BareWireConfigurationException(
                optionName: "Transport",
                optionValue: null,
                expectedValue: "A transport adapter must be registered. " +
                               "Call UseRabbitMQ(...) or register an ITransportAdapter in the DI container.");
        }
    }

    private static void ValidateReceiveEndpoints(BusConfigurator configurator)
    {
        foreach (ReceiveEndpointConfiguration endpoint in configurator.ReceiveEndpoints)
        {
            ValidateEndpointHasConsumer(endpoint);
            ValidatePrefetchCount(endpoint);
            ValidateConcurrentMessageLimit(endpoint);
        }
    }

    private static void ValidateEndpointHasConsumer(ReceiveEndpointConfiguration endpoint)
    {
        if (!endpoint.HasAnyConsumer)
        {
            throw new BareWireConfigurationException(
                optionName: $"ReceiveEndpoint[{endpoint.EndpointName}].Consumers",
                optionValue: "0",
                expectedValue: "Each receive endpoint must have at least one consumer, raw consumer, or state machine saga registered.");
        }
    }

    private static void ValidatePrefetchCount(ReceiveEndpointConfiguration endpoint)
    {
        if (endpoint.PrefetchCount <= 0)
        {
            throw new BareWireConfigurationException(
                optionName: $"ReceiveEndpoint[{endpoint.EndpointName}].PrefetchCount",
                optionValue: endpoint.PrefetchCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                expectedValue: "PrefetchCount must be greater than 0.");
        }
    }

    private static void ValidateConcurrentMessageLimit(ReceiveEndpointConfiguration endpoint)
    {
        if (endpoint.ConcurrentMessageLimit <= 0)
        {
            throw new BareWireConfigurationException(
                optionName: $"ReceiveEndpoint[{endpoint.EndpointName}].ConcurrentMessageLimit",
                optionValue: endpoint.ConcurrentMessageLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
                expectedValue: "ConcurrentMessageLimit must be greater than 0.");
        }
    }
}
