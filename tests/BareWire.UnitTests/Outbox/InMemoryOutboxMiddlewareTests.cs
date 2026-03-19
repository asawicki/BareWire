// NSubstitute's Returns() for ValueTask-returning mocks triggers CA2012 as a false positive.
// The ValueTask is consumed internally by NSubstitute and never double-consumed.
#pragma warning disable CA2012

using System.Buffers;
using AwesomeAssertions;
using BareWire.Abstractions.Pipeline;
using BareWire.Abstractions.Transport;
using BareWire.Outbox;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace BareWire.UnitTests.Outbox;

public sealed class InMemoryOutboxMiddlewareTests
{
    private readonly IOutboxStore _outboxStore;
    private readonly ILogger<InMemoryOutboxMiddleware> _logger;
    private readonly InMemoryOutboxMiddleware _sut;

    public InMemoryOutboxMiddlewareTests()
    {
        _outboxStore = Substitute.For<IOutboxStore>();
        _logger = Substitute.For<ILogger<InMemoryOutboxMiddleware>>();
        _sut = new InMemoryOutboxMiddleware(_outboxStore, _logger);
    }

    private static MessageContext CreateContext(CancellationToken cancellationToken = default)
    {
        IServiceProvider serviceProvider = Substitute.For<IServiceProvider>();
        return new MessageContext(
            messageId: Guid.NewGuid(),
            headers: new Dictionary<string, string>(),
            rawBody: ReadOnlySequence<byte>.Empty,
            serviceProvider: serviceProvider,
            cancellationToken: cancellationToken);
    }

    private static OutboundMessage CreateOutboundMessage(string routingKey = "test.routing.key") =>
        new(
            routingKey: routingKey,
            headers: new Dictionary<string, string>(),
            body: ReadOnlyMemory<byte>.Empty,
            contentType: "application/json");

    [Fact]
    public async Task InvokeAsync_HandlerSuccess_FlushesBufferedMessages()
    {
        // Arrange
        var context = CreateContext();
        IReadOnlyList<OutboundMessage>? capturedMessages = null;

        _outboxStore
            .SaveMessagesAsync(Arg.Any<IReadOnlyList<OutboundMessage>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                capturedMessages = call.Arg<IReadOnlyList<OutboundMessage>>();
                return ValueTask.CompletedTask;
            });

        NextMiddleware next = _ =>
        {
            var buffer = InMemoryOutboxMiddleware.Current;
            buffer!.Add(CreateOutboundMessage("key.1"));
            buffer.Add(CreateOutboundMessage("key.2"));
            buffer.Add(CreateOutboundMessage("key.3"));
            return Task.CompletedTask;
        };

        // Act
        await _sut.InvokeAsync(context, next);

        // Assert
        await _outboxStore.Received(1).SaveMessagesAsync(
            Arg.Any<IReadOnlyList<OutboundMessage>>(),
            Arg.Any<CancellationToken>());

        capturedMessages.Should().NotBeNull();
        capturedMessages!.Count.Should().Be(3);
        capturedMessages[0].RoutingKey.Should().Be("key.1");
        capturedMessages[1].RoutingKey.Should().Be("key.2");
        capturedMessages[2].RoutingKey.Should().Be("key.3");
    }

    [Fact]
    public async Task InvokeAsync_HandlerThrows_DiscardsBuffer()
    {
        // Arrange
        var context = CreateContext();

        NextMiddleware next = _ =>
        {
            var buffer = InMemoryOutboxMiddleware.Current;
            buffer!.Add(CreateOutboundMessage());
            throw new InvalidOperationException("handler failure");
        };

        // Act
        Func<Task> act = () => _sut.InvokeAsync(context, next);

        // Assert
        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage("handler failure");

        await _outboxStore.DidNotReceive().SaveMessagesAsync(
            Arg.Any<IReadOnlyList<OutboundMessage>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_NoPublish_NoFlush()
    {
        // Arrange
        var context = CreateContext();

        NextMiddleware next = _ => Task.CompletedTask;

        // Act
        await _sut.InvokeAsync(context, next);

        // Assert
        await _outboxStore.DidNotReceive().SaveMessagesAsync(
            Arg.Any<IReadOnlyList<OutboundMessage>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_NestedPublish_AllBuffered()
    {
        // Arrange
        var context = CreateContext();
        IReadOnlyList<OutboundMessage>? capturedMessages = null;

        _outboxStore
            .SaveMessagesAsync(Arg.Any<IReadOnlyList<OutboundMessage>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                capturedMessages = call.Arg<IReadOnlyList<OutboundMessage>>();
                return ValueTask.CompletedTask;
            });

        NextMiddleware next = async _ =>
        {
            var buffer = InMemoryOutboxMiddleware.Current;
            buffer!.Add(CreateOutboundMessage("outer.1"));

            // Simulate nested async call that also publishes to same buffer
            await Task.Yield();
            var bufferFromNested = InMemoryOutboxMiddleware.Current;
            bufferFromNested!.Add(CreateOutboundMessage("nested.1"));
            bufferFromNested.Add(CreateOutboundMessage("nested.2"));
        };

        // Act
        await _sut.InvokeAsync(context, next);

        // Assert
        await _outboxStore.Received(1).SaveMessagesAsync(
            Arg.Any<IReadOnlyList<OutboundMessage>>(),
            Arg.Any<CancellationToken>());

        capturedMessages.Should().NotBeNull();
        capturedMessages!.Count.Should().Be(3);
    }

    [Fact]
    public async Task InvokeAsync_CancellationRequested_PropagatesWithoutFlush()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var context = CreateContext(cts.Token);

        NextMiddleware next = _ => Task.FromCanceled(cts.Token);

        // Act
        Func<Task> act = () => _sut.InvokeAsync(context, next);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        await _outboxStore.DidNotReceive().SaveMessagesAsync(
            Arg.Any<IReadOnlyList<OutboundMessage>>(),
            Arg.Any<CancellationToken>());
    }
}
