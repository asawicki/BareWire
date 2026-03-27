using AwesomeAssertions;
using BareWire.Abstractions;
using BareWire.Abstractions.Transport;
using BareWire;
using BareWire.Serialization.Json;
using BareWire.Transport.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;

namespace BareWire.UnitTests.Core;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddBareWire_WithoutTransportRegistration_ThrowsOnResolve()
    {
        // Arrange — AddBareWire without AddBareWireRabbitMq
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBareWireJsonSerializer();
        services.AddBareWire(cfg =>
        {
            cfg.UseRabbitMQ(rmq =>
            {
                rmq.Host("amqp://guest:guest@localhost:5672/");
            });
        });

        // Act — building the provider succeeds, but resolving the bus fails
        // because ITransportAdapter is not registered
        using var provider = services.BuildServiceProvider();
        Action act = () => provider.GetRequiredService<IBus>();

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task AddBareWire_WithRabbitMqTransport_ResolvesIBus()
    {
        // Arrange — register transport THEN bus
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBareWireJsonSerializer();

        services.AddBareWireRabbitMq(rmq =>
        {
            rmq.Host("amqp://guest:guest@localhost:5672/");
        });

        services.AddBareWire(cfg =>
        {
            cfg.UseRabbitMQ(rmq =>
            {
                rmq.Host("amqp://guest:guest@localhost:5672/");
            });
        });

        // Act
        await using var provider = services.BuildServiceProvider();
        var bus = provider.GetService<IBus>();

        // Assert
        bus.Should().NotBeNull();
    }

    [Fact]
    public async Task AddBareWire_WithRabbitMqTransport_ResolvesIPublishEndpoint()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBareWireJsonSerializer();

        services.AddBareWireRabbitMq(rmq =>
        {
            rmq.Host("amqp://guest:guest@localhost:5672/");
        });

        services.AddBareWire(cfg =>
        {
            cfg.UseRabbitMQ(rmq =>
            {
                rmq.Host("amqp://guest:guest@localhost:5672/");
            });
        });

        // Act
        await using var provider = services.BuildServiceProvider();
        var publishEndpoint = provider.GetService<IPublishEndpoint>();

        // Assert
        publishEndpoint.Should().NotBeNull();
    }
}
