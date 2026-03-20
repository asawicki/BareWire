using AwesomeAssertions;
using BareWire.Abstractions;
using BareWire.Abstractions.Serialization;
using BareWire.Abstractions.Transport;
using BareWire.Core.Bus;
using BareWire.Core.FlowControl;
using BareWire.Core.Observability;
using BareWire.Core.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace BareWire.UnitTests.Core.Bus;

// Must be public so NSubstitute's Castle proxy can create IRequestClient<PingRequest>.
public sealed record PingRequest(string Value);

public sealed class BareWireBusCreateRequestClientTests
{

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static BareWireBus CreateBus(IRequestClientFactory? factory = null)
    {
        ITransportAdapter adapter = Substitute.For<ITransportAdapter>();
        adapter.TransportName.Returns("test");

        IMessageSerializer serializer = Substitute.For<IMessageSerializer>();
        serializer.ContentType.Returns("application/json");

        IMessageDeserializer deserializer = Substitute.For<IMessageDeserializer>();
        IServiceScopeFactory scopeFactory = Substitute.For<IServiceScopeFactory>();

        MiddlewareChain chain = new([]);
        ConsumerDispatcher dispatcher = new(scopeFactory, NullLogger<ConsumerDispatcher>.Instance);
        MessagePipeline pipeline = new(
            chain,
            dispatcher,
            deserializer,
            NullLogger<MessagePipeline>.Instance,
            new NullInstrumentation());
        FlowController flowController = new(NullLogger<FlowController>.Instance);

        return new BareWireBus(
            adapter,
            serializer,
            pipeline,
            flowController,
            new PublishFlowControlOptions(),
            NullLogger<BareWireBus>.Instance,
            new NullInstrumentation(),
            factory);
    }

    // ── CreateRequestClient ───────────────────────────────────────────────────

    [Fact]
    public void CreateRequestClient_WhenFactoryRegistered_DelegatesToFactory()
    {
        // Arrange
        IRequestClient<PingRequest> expectedClient = Substitute.For<IRequestClient<PingRequest>>();
        IRequestClientFactory factory = Substitute.For<IRequestClientFactory>();
        factory.CreateRequestClient<PingRequest>().Returns(expectedClient);

        BareWireBus bus = CreateBus(factory);

        // Act
        IRequestClient<PingRequest> result = bus.CreateRequestClient<PingRequest>();

        // Assert
        result.Should().BeSameAs(expectedClient);
        factory.Received(1).CreateRequestClient<PingRequest>();
    }

    [Fact]
    public void CreateRequestClient_WhenNoFactoryRegistered_ThrowsNotSupportedException()
    {
        // Arrange — no factory, null is passed
        BareWireBus bus = CreateBus(factory: null);

        // Act
        Action act = () => bus.CreateRequestClient<PingRequest>();

        // Assert
        act.Should().Throw<NotSupportedException>()
           .WithMessage("*IRequestClientFactory*");
    }
}
