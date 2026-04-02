using System.Buffers;
using AwesomeAssertions;
using BareWire.Abstractions;
using BareWire.Abstractions.Exceptions;
using BareWire.Abstractions.Serialization;
using BareWire.Bus;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace BareWire.UnitTests.Core.Bus;

// Must be public so ConsumerInvokerFactory can build generic delegates over these types.
public sealed record InvokerTestMessage(string Value);

public sealed class InvokerTestConsumer : IConsumer<InvokerTestMessage>
{
    public bool WasCalled { get; private set; }
    public ConsumeContext<InvokerTestMessage>? LastContext { get; private set; }

    public Task ConsumeAsync(ConsumeContext<InvokerTestMessage> context)
    {
        WasCalled = true;
        LastContext = context;
        return Task.CompletedTask;
    }
}

public sealed class InvokerRawTestConsumer : IRawConsumer
{
    public bool WasCalled { get; private set; }
    public RawConsumeContext? LastContext { get; private set; }

    public Task ConsumeAsync(RawConsumeContext context)
    {
        WasCalled = true;
        LastContext = context;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Tests for <see cref="ConsumerInvokerFactory"/> targeting surviving mutants:
/// MUT-398 (CreateTyped block removal), MUT-402 (null check inversion), MUT-410 (CreateRawTyped block removal).
/// </summary>
public sealed class ConsumerInvokerFactoryTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (IServiceScopeFactory ScopeFactory, InvokerTestConsumer Consumer)
        BuildTypedScopeFactory()
    {
        InvokerTestConsumer consumer = new();

        IServiceScopeFactory scopeFactory = Substitute.For<IServiceScopeFactory>();
        IServiceScope scope = Substitute.For<IServiceScope>();
        IServiceProvider provider = Substitute.For<IServiceProvider>();

        scopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(provider);
        provider.GetService(typeof(InvokerTestConsumer)).Returns(consumer);

        return (scopeFactory, consumer);
    }

    private static (IServiceScopeFactory ScopeFactory, InvokerRawTestConsumer Consumer)
        BuildRawScopeFactory()
    {
        InvokerRawTestConsumer consumer = new();

        IServiceScopeFactory scopeFactory = Substitute.For<IServiceScopeFactory>();
        IServiceScope scope = Substitute.For<IServiceScope>();
        IServiceProvider provider = Substitute.For<IServiceProvider>();

        scopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(provider);
        provider.GetService(typeof(InvokerRawTestConsumer)).Returns(consumer);

        return (scopeFactory, consumer);
    }

    private static IDeserializerResolver BuildDeserializerResolver(InvokerTestMessage? returnValue)
    {
        IMessageDeserializer deserializer = Substitute.For<IMessageDeserializer>();
        deserializer.ContentType.Returns("application/json");
        deserializer.Deserialize<InvokerTestMessage>(Arg.Any<ReadOnlySequence<byte>>())
                    .Returns(returnValue);

        IDeserializerResolver resolver = Substitute.For<IDeserializerResolver>();
        resolver.Resolve(Arg.Any<string?>()).Returns(deserializer);
        return resolver;
    }

    // ── MUT-398: CreateTyped block removal ────────────────────────────────────
    // If CreateTyped returns default (null delegate) instead of InvokeTypedConsumerAsync,
    // calling the returned invoker would NullReferenceException or do nothing.
    // This test verifies the consumer's ConsumeAsync is actually called.

    [Fact]
    public async Task Create_WhenInvoked_ActuallyCallsConsumerConsumeAsync()
    {
        // Arrange
        ConsumerInvokerFactory.InvokerDelegate invoker =
            ConsumerInvokerFactory.Create(typeof(InvokerTestConsumer), typeof(InvokerTestMessage));

        var (scopeFactory, consumer) = BuildTypedScopeFactory();
        IDeserializerResolver resolver = BuildDeserializerResolver(new InvokerTestMessage("hello"));
        IPublishEndpoint pub = Substitute.For<IPublishEndpoint>();
        ISendEndpointProvider send = Substitute.For<ISendEndpointProvider>();
        var headers = new Dictionary<string, string>();

        // Act
        await invoker(scopeFactory, ReadOnlySequence<byte>.Empty, headers,
            Guid.NewGuid().ToString(), pub, send, resolver, "test-ep", CancellationToken.None);

        // Assert — MUT-398: consumer must have been invoked (block removal would cause no call)
        consumer.WasCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Create_WhenInvoked_PassesMessageToConsumer()
    {
        // Arrange
        ConsumerInvokerFactory.InvokerDelegate invoker =
            ConsumerInvokerFactory.Create(typeof(InvokerTestConsumer), typeof(InvokerTestMessage));

        var (scopeFactory, consumer) = BuildTypedScopeFactory();
        InvokerTestMessage expectedMessage = new("hello-world");
        IDeserializerResolver resolver = BuildDeserializerResolver(expectedMessage);
        IPublishEndpoint pub = Substitute.For<IPublishEndpoint>();
        ISendEndpointProvider send = Substitute.For<ISendEndpointProvider>();
        var headers = new Dictionary<string, string>();

        // Act
        await invoker(scopeFactory, ReadOnlySequence<byte>.Empty, headers,
            Guid.NewGuid().ToString(), pub, send, resolver, "test-ep", CancellationToken.None);

        // Assert — the consumer received the correct message
        consumer.LastContext.Should().NotBeNull();
        consumer.LastContext!.Message.Should().Be(expectedMessage);
    }

    // ── MUT-402: null check inversion (msg is null → msg is not null) ─────────
    // When deserializer returns null, the invoker must throw UnknownPayloadException.
    // When deserializer returns a valid message, it must NOT throw.

    [Fact]
    public async Task Create_WhenDeserializerReturnsNull_ThrowsUnknownPayloadException()
    {
        // Arrange
        ConsumerInvokerFactory.InvokerDelegate invoker =
            ConsumerInvokerFactory.Create(typeof(InvokerTestConsumer), typeof(InvokerTestMessage));

        var (scopeFactory, _) = BuildTypedScopeFactory();
        IDeserializerResolver resolver = BuildDeserializerResolver(returnValue: null);
        IPublishEndpoint pub = Substitute.For<IPublishEndpoint>();
        ISendEndpointProvider send = Substitute.For<ISendEndpointProvider>();
        var headers = new Dictionary<string, string>();

        // Act
        var act = async () => await invoker(scopeFactory, ReadOnlySequence<byte>.Empty, headers,
            Guid.NewGuid().ToString(), pub, send, resolver, "test-ep", CancellationToken.None);

        // Assert — MUT-402: null body must trigger exception
        await act.Should().ThrowAsync<UnknownPayloadException>();
    }

    [Fact]
    public async Task Create_WhenDeserializerReturnsNull_ThrowsWithCorrectEndpointName()
    {
        // Arrange
        ConsumerInvokerFactory.InvokerDelegate invoker =
            ConsumerInvokerFactory.Create(typeof(InvokerTestConsumer), typeof(InvokerTestMessage));

        var (scopeFactory, _) = BuildTypedScopeFactory();
        IDeserializerResolver resolver = BuildDeserializerResolver(returnValue: null);
        IPublishEndpoint pub = Substitute.For<IPublishEndpoint>();
        ISendEndpointProvider send = Substitute.For<ISendEndpointProvider>();
        var headers = new Dictionary<string, string>();

        // Act
        var act = async () => await invoker(scopeFactory, ReadOnlySequence<byte>.Empty, headers,
            Guid.NewGuid().ToString(), pub, send, resolver, "my-queue", CancellationToken.None);

        // Assert — exception carries the endpoint name
        (await act.Should().ThrowAsync<UnknownPayloadException>())
            .Which.EndpointName.Should().Be("my-queue");
    }

    [Fact]
    public async Task Create_WhenDeserializerReturnsValidMessage_DoesNotThrow()
    {
        // Arrange
        ConsumerInvokerFactory.InvokerDelegate invoker =
            ConsumerInvokerFactory.Create(typeof(InvokerTestConsumer), typeof(InvokerTestMessage));

        var (scopeFactory, _) = BuildTypedScopeFactory();
        IDeserializerResolver resolver = BuildDeserializerResolver(new InvokerTestMessage("ok"));
        IPublishEndpoint pub = Substitute.For<IPublishEndpoint>();
        ISendEndpointProvider send = Substitute.For<ISendEndpointProvider>();
        var headers = new Dictionary<string, string>();

        // Act
        var act = async () => await invoker(scopeFactory, ReadOnlySequence<byte>.Empty, headers,
            Guid.NewGuid().ToString(), pub, send, resolver, "test-ep", CancellationToken.None);

        // Assert — MUT-402: valid message must NOT throw
        await act.Should().NotThrowAsync<UnknownPayloadException>();
    }

    // ── MUT-410: CreateRawTyped block removal ─────────────────────────────────
    // If CreateRawTyped returns default (null delegate) instead of InvokeRawConsumerAsync,
    // calling the returned raw invoker would NullReferenceException or do nothing.
    // This test verifies the raw consumer's ConsumeAsync is actually called.

    [Fact]
    public async Task CreateRaw_WhenInvoked_ActuallyCallsRawConsumerConsumeAsync()
    {
        // Arrange
        ConsumerInvokerFactory.RawInvokerDelegate rawInvoker =
            ConsumerInvokerFactory.CreateRaw(typeof(InvokerRawTestConsumer));

        var (scopeFactory, consumer) = BuildRawScopeFactory();

        IMessageDeserializer deserializer = Substitute.For<IMessageDeserializer>();
        deserializer.ContentType.Returns("application/json");
        IDeserializerResolver resolver = Substitute.For<IDeserializerResolver>();
        resolver.Resolve(Arg.Any<string?>()).Returns(deserializer);

        IPublishEndpoint pub = Substitute.For<IPublishEndpoint>();
        ISendEndpointProvider send = Substitute.For<ISendEndpointProvider>();
        var headers = new Dictionary<string, string>();

        // Act
        await rawInvoker(scopeFactory, ReadOnlySequence<byte>.Empty, headers,
            Guid.NewGuid().ToString(), pub, send, resolver, CancellationToken.None);

        // Assert — MUT-410: raw consumer must have been invoked (block removal would cause no call)
        consumer.WasCalled.Should().BeTrue();
    }

    [Fact]
    public async Task CreateRaw_WhenInvoked_PassesBodyToRawConsumer()
    {
        // Arrange
        ConsumerInvokerFactory.RawInvokerDelegate rawInvoker =
            ConsumerInvokerFactory.CreateRaw(typeof(InvokerRawTestConsumer));

        var (scopeFactory, consumer) = BuildRawScopeFactory();

        IMessageDeserializer deserializer = Substitute.For<IMessageDeserializer>();
        deserializer.ContentType.Returns("application/json");
        IDeserializerResolver resolver = Substitute.For<IDeserializerResolver>();
        resolver.Resolve(Arg.Any<string?>()).Returns(deserializer);

        IPublishEndpoint pub = Substitute.For<IPublishEndpoint>();
        ISendEndpointProvider send = Substitute.For<ISendEndpointProvider>();
        var headers = new Dictionary<string, string>();
        byte[] bodyBytes = [1, 2, 3, 4];
        ReadOnlySequence<byte> body = new(bodyBytes);

        // Act
        await rawInvoker(scopeFactory, body, headers,
            Guid.NewGuid().ToString(), pub, send, resolver, CancellationToken.None);

        // Assert — body was forwarded to the raw consumer context
        consumer.LastContext.Should().NotBeNull();
        consumer.LastContext!.RawBody.ToArray().Should().BeEquivalentTo(bodyBytes);
    }
}
