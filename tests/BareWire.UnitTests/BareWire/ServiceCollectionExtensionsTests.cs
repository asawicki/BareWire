using AwesomeAssertions;
using BareWire.Abstractions;
using BareWire.Abstractions.Observability;
using BareWire.Abstractions.Pipeline;
using BareWire.Abstractions.Routing;
using BareWire.Abstractions.Transport;
using BareWire.Bus;
using BareWire.Configuration;
using BareWire.FlowControl;
using BareWire.Pipeline;
using BareWire.Routing;
using BareWire.Serialization.Json;
using BareWire.Transport.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BareWire.UnitTests.Core;

public sealed class ServiceCollectionExtensionsTests
{
    // ── Group 1: Argument guards ─────────────────────────────────────────────

    [Fact]
    public void AddBareWire_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection? services = null;

        var act = () => services!.AddBareWire(_ => { });

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("services");
    }

    [Fact]
    public void AddBareWire_NullConfigure_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        var act = () => services.AddBareWire(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("configure");
    }

    // ── Group 2: Configure invocation ────────────────────────────────────────

    [Fact]
    public void AddBareWire_InvokesConfigure_WithConfigurator()
    {
        var services = new ServiceCollection();
        var configuratorSeen = false;

        services.AddBareWire(cfg =>
        {
            configuratorSeen = true;
            cfg.Should().NotBeNull();
        });

        configuratorSeen.Should().BeTrue();
    }

    // ── Group 3: RegisterMiddleware call ─────────────────────────────────────

    [Fact]
    public void AddBareWire_WithMiddleware_RegistersMiddlewareInDI()
    {
        var services = new ServiceCollection();

        services.AddBareWire(cfg =>
        {
            cfg.AddMiddleware<RetryMiddleware>();
        });

        services.Any(d => d.ServiceType == typeof(RetryMiddleware))
            .Should().BeTrue();
    }

    // ── Group 4: Core service descriptors ────────────────────────────────────

    [Fact]
    public void AddBareWire_Always_RegistersBusConfigurator()
    {
        var services = new ServiceCollection();
        services.AddBareWire(_ => { });

        services.Any(d => d.ServiceType == typeof(BusConfigurator))
            .Should().BeTrue();
    }

    [Fact]
    public void AddBareWire_Always_RegistersNullInstrumentationFallback()
    {
        var services = new ServiceCollection();
        services.AddBareWire(_ => { });

        services.Any(d => d.ServiceType == typeof(IBareWireInstrumentation))
            .Should().BeTrue();
    }

    [Fact]
    public void AddBareWire_Always_RegistersFlowController()
    {
        var services = new ServiceCollection();
        services.AddBareWire(_ => { });

        services.Any(d => d.ServiceType == typeof(FlowController))
            .Should().BeTrue();
    }

    [Fact]
    public void AddBareWire_Always_RegistersMiddlewareChain()
    {
        var services = new ServiceCollection();
        services.AddBareWire(_ => { });

        services.Any(d => d.ServiceType == typeof(MiddlewareChain))
            .Should().BeTrue();
    }

    [Fact]
    public void AddBareWire_Always_RegistersMessagePipeline()
    {
        var services = new ServiceCollection();
        services.AddBareWire(_ => { });

        services.Any(d => d.ServiceType == typeof(MessagePipeline))
            .Should().BeTrue();
    }

    [Fact]
    public void AddBareWire_Always_RegistersRoutingKeyResolver()
    {
        var services = new ServiceCollection();
        services.AddBareWire(_ => { });

        services.Any(d => d.ServiceType == typeof(IRoutingKeyResolver))
            .Should().BeTrue();
    }

    [Fact]
    public void AddBareWire_Always_RegistersPublishFlowControlOptions()
    {
        var services = new ServiceCollection();
        services.AddBareWire(_ => { });

        services.Any(d => d.ServiceType == typeof(PublishFlowControlOptions))
            .Should().BeTrue();
    }

    [Fact]
    public void AddBareWire_Always_RegistersBareWireBus()
    {
        var services = new ServiceCollection();
        services.AddBareWire(_ => { });

        services.Any(d => d.ServiceType == typeof(BareWireBus))
            .Should().BeTrue();
    }

    [Fact]
    public void AddBareWire_Always_RegistersBareWireBusControl()
    {
        var services = new ServiceCollection();
        services.AddBareWire(_ => { });

        services.Any(d => d.ServiceType == typeof(BareWireBusControl))
            .Should().BeTrue();
    }

    [Fact]
    public void AddBareWire_Always_RegistersIBusControl()
    {
        var services = new ServiceCollection();
        services.AddBareWire(_ => { });

        services.Any(d => d.ServiceType == typeof(IBusControl))
            .Should().BeTrue();
    }

    [Fact]
    public void AddBareWire_Always_RegistersIBus()
    {
        var services = new ServiceCollection();
        services.AddBareWire(_ => { });

        services.Any(d => d.ServiceType == typeof(IBus))
            .Should().BeTrue();
    }

    [Fact]
    public void AddBareWire_Always_RegistersIPublishEndpoint()
    {
        var services = new ServiceCollection();
        services.AddBareWire(_ => { });

        services.Any(d => d.ServiceType == typeof(IPublishEndpoint))
            .Should().BeTrue();
    }

    [Fact]
    public void AddBareWire_Always_RegistersISendEndpointProvider()
    {
        var services = new ServiceCollection();
        services.AddBareWire(_ => { });

        services.Any(d => d.ServiceType == typeof(ISendEndpointProvider))
            .Should().BeTrue();
    }

    [Fact]
    public void AddBareWire_Always_RegistersBareWireBusHostedService()
    {
        var services = new ServiceCollection();
        services.AddBareWire(_ => { });

        services.Any(d =>
                d.ServiceType == typeof(IHostedService) &&
                d.ImplementationType == typeof(BareWireBusHostedService))
            .Should().BeTrue();
    }

    // ── Group 5: PublishFlowControlOptions conditional registration ──────────

    [Fact]
    public void AddBareWire_WhenPublishFlowControlOptionsPreRegistered_DoesNotOverwrite()
    {
        var services = new ServiceCollection();
        var customOptions = new PublishFlowControlOptions { MaxPendingPublishes = 42 };
        services.AddSingleton(customOptions);

        services.AddBareWire(_ => { });

        // Only one registration should exist
        var registrations = services
            .Where(d => d.ServiceType == typeof(PublishFlowControlOptions))
            .ToList();

        registrations.Should().HaveCount(1);
        registrations[0].ImplementationInstance.Should().BeSameAs(customOptions);
    }

    [Fact]
    public void AddBareWire_WhenNoPublishFlowControlOptions_RegistersDefault()
    {
        var services = new ServiceCollection();

        services.AddBareWire(_ => { });

        var registration = services
            .SingleOrDefault(d => d.ServiceType == typeof(PublishFlowControlOptions));

        registration.Should().NotBeNull();
        registration!.ImplementationInstance.Should().BeOfType<PublishFlowControlOptions>();
    }

    // ── Existing tests (unchanged) ───────────────────────────────────────────

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
