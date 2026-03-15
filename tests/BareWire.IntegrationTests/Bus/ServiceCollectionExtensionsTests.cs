using AwesomeAssertions;
using BareWire.Abstractions;
using BareWire.Abstractions.Configuration;
using BareWire.Abstractions.Exceptions;
using BareWire.Abstractions.Serialization;
using BareWire.Abstractions.Topology;
using BareWire.Abstractions.Transport;
using BareWire.Core;
using BareWire.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace BareWire.IntegrationTests.Bus;

/// <summary>
/// Integration tests for <see cref="ServiceCollectionExtensions.AddBareWire"/>.
/// Uses a real <see cref="ServiceCollection"/> and <see cref="ServiceProvider"/>
/// to verify that all services are registered with the correct lifetimes and that
/// configuration validation fires correctly on bus start.
/// </summary>
public sealed class ServiceCollectionExtensionsTests
{
    // ── Shared fake objects ────────────────────────────────────────────────────

    /// <summary>
    /// Builds a <see cref="ServiceProvider"/> with BareWire registered, backed by
    /// NSubstitute fakes for <see cref="ITransportAdapter"/> and <see cref="IMessageSerializer"/>.
    /// The transport is treated as "InMemory" via <c>HasInMemoryTransport = true</c>
    /// on the configurator.
    /// </summary>
    private static ServiceProvider BuildProvider(Action<ServiceCollection>? extraServices = null)
    {
        ServiceCollection services = new();

        // Register NSubstitute fakes for the two external dependencies BareWireBus needs.
        ITransportAdapter fakeTransport = CreateFakeTransport();
        IMessageSerializer fakeSerializer = CreateFakeSerializer();

        services.AddSingleton(fakeTransport);
        services.AddSingleton(fakeSerializer);

        // IMessageDeserializer is needed by MessagePipeline.
        IMessageDeserializer fakeDeserializer = Substitute.For<IMessageDeserializer>();
        services.AddSingleton(fakeDeserializer);

        extraServices?.Invoke(services);

        services.AddBareWire(cfg =>
        {
            // Signal that a transport is configured (bypasses the "no transport" validation).
            cfg.UseInMemoryTransport();
        });

        // Add logging (required by FlowController, BareWireBusControl, etc.).
        services.AddLogging();

        return services.BuildServiceProvider();
    }

    private static ITransportAdapter CreateFakeTransport()
    {
        ITransportAdapter transport = Substitute.For<ITransportAdapter>();
        transport.TransportName.Returns("InMemory");
        transport.DeployTopologyAsync(Arg.Any<TopologyDeclaration>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        transport.SendBatchAsync(Arg.Any<IReadOnlyList<OutboundMessage>>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                IReadOnlyList<OutboundMessage> msgs = ci.ArgAt<IReadOnlyList<OutboundMessage>>(0);
                IReadOnlyList<SendResult> results = msgs
                    .Select(_ => new SendResult(IsConfirmed: true, DeliveryTag: 0UL))
                    .ToList();
                return Task.FromResult(results);
            });
        return transport;
    }

    private static IMessageSerializer CreateFakeSerializer()
    {
        IMessageSerializer serializer = Substitute.For<IMessageSerializer>();
        serializer.ContentType.Returns("application/json");
        return serializer;
    }

    // ── Service registration tests ─────────────────────────────────────────────

    [Fact]
    public void AddBareWire_RegistersIBusAsSingleton()
    {
        // Arrange
        using ServiceProvider provider = BuildProvider();

        // Act
        IBus first = provider.GetRequiredService<IBus>();
        IBus second = provider.GetRequiredService<IBus>();

        // Assert — same instance (Singleton)
        first.Should().NotBeNull();
        first.Should().BeSameAs(second);
    }

    [Fact]
    public void AddBareWire_RegistersIBusControlAsSingleton()
    {
        // Arrange
        using ServiceProvider provider = BuildProvider();

        // Act
        IBusControl first = provider.GetRequiredService<IBusControl>();
        IBusControl second = provider.GetRequiredService<IBusControl>();

        // Assert
        first.Should().NotBeNull();
        first.Should().BeSameAs(second);
    }

    [Fact]
    public void AddBareWire_IBusAndIBusControl_ResolveSameInstance()
    {
        // Arrange — both IBus and IBusControl should resolve to the same BareWireBusControl object.
        using ServiceProvider provider = BuildProvider();

        // Act
        IBus bus = provider.GetRequiredService<IBus>();
        IBusControl busControl = provider.GetRequiredService<IBusControl>();

        // Assert
        bus.Should().BeSameAs(busControl);
    }

    [Fact]
    public void AddBareWire_RegistersIPublishEndpointAsSingleton()
    {
        // Arrange
        using ServiceProvider provider = BuildProvider();

        // Act
        IPublishEndpoint first = provider.GetRequiredService<IPublishEndpoint>();
        IPublishEndpoint second = provider.GetRequiredService<IPublishEndpoint>();

        // Assert
        first.Should().NotBeNull();
        first.Should().BeSameAs(second);
    }

    [Fact]
    public void AddBareWire_IPublishEndpoint_ForwardsToBus()
    {
        // Arrange
        using ServiceProvider provider = BuildProvider();

        // Act
        IPublishEndpoint publishEndpoint = provider.GetRequiredService<IPublishEndpoint>();
        IBus bus = provider.GetRequiredService<IBus>();

        // Assert — IPublishEndpoint and IBus resolve to the same BareWireBusControl instance.
        publishEndpoint.Should().BeSameAs(bus);
    }

    [Fact]
    public void AddBareWire_RegistersISendEndpointProviderAsSingleton()
    {
        // Arrange
        using ServiceProvider provider = BuildProvider();

        // Act
        ISendEndpointProvider first = provider.GetRequiredService<ISendEndpointProvider>();
        ISendEndpointProvider second = provider.GetRequiredService<ISendEndpointProvider>();

        // Assert
        first.Should().NotBeNull();
        first.Should().BeSameAs(second);
    }

    [Fact]
    public void AddBareWire_ISendEndpointProvider_ForwardsToBus()
    {
        // Arrange
        using ServiceProvider provider = BuildProvider();

        // Act
        ISendEndpointProvider sendProvider = provider.GetRequiredService<ISendEndpointProvider>();
        IBus bus = provider.GetRequiredService<IBus>();

        // Assert
        sendProvider.Should().BeSameAs(bus);
    }

    [Fact]
    public void AddBareWire_RegistersIHostedService()
    {
        // Arrange
        using ServiceProvider provider = BuildProvider();

        // Act
        IHostedService hostedService = provider.GetRequiredService<IHostedService>();

        // Assert
        hostedService.Should().NotBeNull();
    }

    // ── Validation on start tests ──────────────────────────────────────────────

    [Fact]
    public async Task AddBareWire_ValidationError_ThrowsOnStart()
    {
        // Arrange — configure WITHOUT a transport so validation fails.
        ServiceCollection services = new();

        IMessageSerializer fakeSerializer = CreateFakeSerializer();
        ITransportAdapter fakeTransport = CreateFakeTransport();
        IMessageDeserializer fakeDeserializer = Substitute.For<IMessageDeserializer>();

        services.AddSingleton(fakeTransport);
        services.AddSingleton(fakeSerializer);
        services.AddSingleton(fakeDeserializer);
        services.AddLogging();

        // Register BareWire WITHOUT setting any transport — this should fail validation on StartAsync.
        services.AddBareWire(_ => { /* no transport configured */ });

        using ServiceProvider provider = services.BuildServiceProvider();
        IBusControl busControl = provider.GetRequiredService<IBusControl>();

        // Act
        Func<Task> act = () => busControl.StartAsync(CancellationToken.None);

        // Assert — ConfigurationValidator.Validate throws BareWireConfigurationException
        await act.Should().ThrowAsync<BareWireConfigurationException>()
            .WithMessage("*Transport*");
    }

    [Fact]
    public async Task AddBareWire_ValidConfiguration_StartSucceeds()
    {
        // Arrange
        using ServiceProvider provider = BuildProvider();
        IBusControl busControl = provider.GetRequiredService<IBusControl>();

        // Act
        Func<Task> act = async () =>
        {
            BusHandle handle = await busControl.StartAsync(CancellationToken.None);
            handle.Should().NotBeNull();
            await busControl.StopAsync(CancellationToken.None);
        };

        // Assert — no exception thrown
        await act.Should().NotThrowAsync();
    }
}

/// <summary>
/// Extension method on <see cref="IBusConfigurator"/> used by test setup to flag
/// InMemory transport without requiring the full BareWire.Testing project.
/// </summary>
internal static class BusConfiguratorTestExtensions
{
    /// <summary>
    /// Marks the bus as having an InMemory transport registered.
    /// This satisfies <see cref="ConfigurationValidator"/> without a real transport adapter.
    /// </summary>
    internal static void UseInMemoryTransport(this IBusConfigurator configurator)
    {
        ArgumentNullException.ThrowIfNull(configurator);

        if (configurator is BusConfigurator busConfigurator)
            busConfigurator.HasInMemoryTransport = true;
    }
}
