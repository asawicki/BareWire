using System.Buffers;
using AwesomeAssertions;
using BareWire.Abstractions;
using BareWire.Abstractions.Serialization;
using BareWire.Core.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace BareWire.UnitTests.Core.Pipeline;

// Must be public for NSubstitute to proxy IConsumer<TestMessage>.
public sealed record TestMessage(string Value);

public sealed class ConsumerDispatcherTests
{

    private static ConsumeContext<T> CreateConsumeContext<T>(T message) where T : class
    {
        IPublishEndpoint publishEndpoint = Substitute.For<IPublishEndpoint>();
        ISendEndpointProvider sendEndpointProvider = Substitute.For<ISendEndpointProvider>();

        return new ConsumeContext<T>(
            message: message,
            messageId: Guid.NewGuid(),
            correlationId: null,
            conversationId: null,
            sourceAddress: null,
            destinationAddress: null,
            sentTime: null,
            headers: new Dictionary<string, string>(),
            contentType: null,
            rawBody: ReadOnlySequence<byte>.Empty,
            publishEndpoint: publishEndpoint,
            sendEndpointProvider: sendEndpointProvider);
    }

    private static RawConsumeContext CreateRawConsumeContext()
    {
        IPublishEndpoint publishEndpoint = Substitute.For<IPublishEndpoint>();
        ISendEndpointProvider sendEndpointProvider = Substitute.For<ISendEndpointProvider>();
        IMessageDeserializer deserializer = Substitute.For<IMessageDeserializer>();

        return new RawConsumeContext(
            messageId: Guid.NewGuid(),
            correlationId: null,
            conversationId: null,
            sourceAddress: null,
            destinationAddress: null,
            sentTime: null,
            headers: new Dictionary<string, string>(),
            contentType: null,
            rawBody: ReadOnlySequence<byte>.Empty,
            publishEndpoint: publishEndpoint,
            sendEndpointProvider: sendEndpointProvider,
            deserializer: deserializer);
    }

    private static (ConsumerDispatcher Dispatcher, IServiceScopeFactory ScopeFactory, IServiceScope Scope, IServiceProvider ScopeProvider)
        CreateDispatcherWithMocks()
    {
        IServiceProvider scopeProvider = Substitute.For<IServiceProvider>();
        IServiceScope scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(scopeProvider);

        IServiceScopeFactory scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        var dispatcher = new ConsumerDispatcher(scopeFactory, NullLogger<ConsumerDispatcher>.Instance);
        return (dispatcher, scopeFactory, scope, scopeProvider);
    }

    [Fact]
    public async Task DispatchAsync_ResolvesConsumerFromScope()
    {
        var (dispatcher, _, _, scopeProvider) = CreateDispatcherWithMocks();
        IConsumer<TestMessage> consumer = Substitute.For<IConsumer<TestMessage>>();
        consumer.ConsumeAsync(Arg.Any<ConsumeContext<TestMessage>>()).Returns(Task.CompletedTask);
        scopeProvider.GetService(typeof(IConsumer<TestMessage>)).Returns(consumer);

        ConsumeContext<TestMessage> context = CreateConsumeContext(new TestMessage("hello"));
        await dispatcher.DispatchAsync(context);

        await consumer.Received(1).ConsumeAsync(context);
    }

    [Fact]
    public async Task DispatchAsync_CreatesNewScopePerDispatch()
    {
        var (dispatcher, scopeFactory, scope, scopeProvider) = CreateDispatcherWithMocks();
        IConsumer<TestMessage> consumer = Substitute.For<IConsumer<TestMessage>>();
        consumer.ConsumeAsync(Arg.Any<ConsumeContext<TestMessage>>()).Returns(Task.CompletedTask);
        scopeProvider.GetService(typeof(IConsumer<TestMessage>)).Returns(consumer);

        ConsumeContext<TestMessage> context = CreateConsumeContext(new TestMessage("hello"));

        await dispatcher.DispatchAsync(context);
        await dispatcher.DispatchAsync(context);

        scopeFactory.Received(2).CreateScope();
    }

    [Fact]
    public async Task DispatchAsync_ConsumerThrows_PropagatesException()
    {
        var (dispatcher, _, _, scopeProvider) = CreateDispatcherWithMocks();
        IConsumer<TestMessage> consumer = Substitute.For<IConsumer<TestMessage>>();
        consumer.ConsumeAsync(Arg.Any<ConsumeContext<TestMessage>>())
            .Returns<Task>(_ => throw new InvalidOperationException("consumer failed"));
        scopeProvider.GetService(typeof(IConsumer<TestMessage>)).Returns(consumer);

        ConsumeContext<TestMessage> context = CreateConsumeContext(new TestMessage("hello"));

        Func<Task> act = () => dispatcher.DispatchAsync(context);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("consumer failed");
    }

    [Fact]
    public async Task DispatchRawAsync_ResolvesRawConsumer()
    {
        var (dispatcher, _, _, scopeProvider) = CreateDispatcherWithMocks();
        IRawConsumer rawConsumer = Substitute.For<IRawConsumer>();
        rawConsumer.ConsumeAsync(Arg.Any<RawConsumeContext>()).Returns(Task.CompletedTask);
        scopeProvider.GetService(typeof(IRawConsumer)).Returns(rawConsumer);

        RawConsumeContext context = CreateRawConsumeContext();
        await dispatcher.DispatchRawAsync(context);

        await rawConsumer.Received(1).ConsumeAsync(context);
    }
}
