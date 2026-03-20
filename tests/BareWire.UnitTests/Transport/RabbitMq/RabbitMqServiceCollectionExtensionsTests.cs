using AwesomeAssertions;
using BareWire.Abstractions.Transport;
using BareWire.Transport.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;

namespace BareWire.UnitTests.Transport.RabbitMq;

public sealed class RabbitMqServiceCollectionExtensionsTests
{
    [Fact]
    public async Task AddBareWireRabbitMq_RegistersITransportAdapter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddBareWireRabbitMq(rmq =>
        {
            rmq.Host("amqp://guest:guest@localhost:5672/");
        });

        // Assert
        await using var provider = services.BuildServiceProvider();
        var adapter = provider.GetService<ITransportAdapter>();
        adapter.Should().NotBeNull();
        adapter.Should().BeOfType<RabbitMqTransportAdapter>();
    }

    [Fact]
    public async Task AddBareWireRabbitMq_CalledTwice_UsesFirstRegistration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act — TryAddSingleton should keep the first registration
        services.AddBareWireRabbitMq(rmq => rmq.Host("amqp://first@localhost:5672/"));
        services.AddBareWireRabbitMq(rmq => rmq.Host("amqp://second@localhost:5672/"));

        // Assert
        await using var provider = services.BuildServiceProvider();
        var adapter = provider.GetService<ITransportAdapter>();
        adapter.Should().NotBeNull();
    }

    [Fact]
    public void AddBareWireRabbitMq_NullServices_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => ServiceCollectionExtensions.AddBareWireRabbitMq(null!, _ => { });

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("services");
    }

    [Fact]
    public void AddBareWireRabbitMq_NullConfigure_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        Action act = () => services.AddBareWireRabbitMq(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("configure");
    }
}
