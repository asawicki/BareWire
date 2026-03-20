using System.Buffers;
using AwesomeAssertions;
using NSubstitute;
using BareWire.Abstractions;

namespace BareWire.UnitTests.Abstractions;

/// <summary>
/// A concrete subclass of <see cref="ConsumeContext"/> that does NOT override
/// <see cref="ConsumeContext.RespondAsync{T}"/>, allowing direct testing of the base implementation.
/// </summary>
internal sealed class RespondTestableConsumeContext(
    Guid messageId,
    Guid? correlationId,
    Guid? conversationId,
    Uri? sourceAddress,
    Uri? destinationAddress,
    DateTimeOffset? sentTime,
    IReadOnlyDictionary<string, string> headers,
    string? contentType,
    ReadOnlySequence<byte> rawBody,
    IPublishEndpoint publishEndpoint,
    ISendEndpointProvider sendEndpointProvider,
    CancellationToken cancellationToken = default)
    : ConsumeContext(messageId, correlationId, conversationId, sourceAddress, destinationAddress,
                     sentTime, headers, contentType, rawBody, publishEndpoint, sendEndpointProvider,
                     cancellationToken);

/// <summary>
/// A concrete subclass of the abstract <see cref="ConsumeContext"/> that exposes the
/// internal constructor for unit testing purposes.
/// </summary>
internal sealed class TestableConsumeContext(
    Guid messageId,
    Guid? correlationId,
    Guid? conversationId,
    Uri? sourceAddress,
    Uri? destinationAddress,
    DateTimeOffset? sentTime,
    IReadOnlyDictionary<string, string> headers,
    string? contentType,
    ReadOnlySequence<byte> rawBody,
    IPublishEndpoint publishEndpoint,
    ISendEndpointProvider sendEndpointProvider,
    CancellationToken cancellationToken = default)
    : ConsumeContext(messageId, correlationId, conversationId, sourceAddress, destinationAddress,
                     sentTime, headers, contentType, rawBody, publishEndpoint, sendEndpointProvider,
                     cancellationToken)
{
    public override Task RespondAsync<T>(T response, CancellationToken ct = default)
        => PublishAsync(response, ct);
}

public sealed class ConsumeContextTests
{
    private static TestableConsumeContext CreateContext(
        Guid? messageId = null,
        Guid? correlationId = null,
        Guid? conversationId = null,
        Uri? sourceAddress = null,
        Uri? destinationAddress = null,
        DateTimeOffset? sentTime = null,
        IReadOnlyDictionary<string, string>? headers = null,
        string? contentType = null,
        ReadOnlySequence<byte> rawBody = default,
        IPublishEndpoint? publishEndpoint = null,
        ISendEndpointProvider? sendEndpointProvider = null,
        CancellationToken cancellationToken = default)
    {
        return new TestableConsumeContext(
            messageId ?? Guid.NewGuid(),
            correlationId,
            conversationId,
            sourceAddress,
            destinationAddress,
            sentTime,
            headers ?? new Dictionary<string, string>(),
            contentType,
            rawBody,
            publishEndpoint ?? Substitute.For<IPublishEndpoint>(),
            sendEndpointProvider ?? Substitute.For<ISendEndpointProvider>(),
            cancellationToken);
    }

    [Fact]
    public void Constructor_WithValidParameters_SetsAllProperties()
    {
        var messageId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var sourceAddress = new Uri("rabbitmq://localhost/source");
        var destinationAddress = new Uri("rabbitmq://localhost/dest");
        var sentTime = DateTimeOffset.UtcNow;
        var headers = new Dictionary<string, string> { ["x-custom"] = "value" };
        var contentType = "application/json";
        var rawBodyBytes = "hello"u8.ToArray();
        var rawBody = new ReadOnlySequence<byte>(rawBodyBytes);

        var ctx = CreateContext(
            messageId: messageId,
            correlationId: correlationId,
            conversationId: conversationId,
            sourceAddress: sourceAddress,
            destinationAddress: destinationAddress,
            sentTime: sentTime,
            headers: headers,
            contentType: contentType,
            rawBody: rawBody);

        ctx.MessageId.Should().Be(messageId);
        ctx.CorrelationId.Should().Be(correlationId);
        ctx.ConversationId.Should().Be(conversationId);
        ctx.SourceAddress.Should().Be(sourceAddress);
        ctx.DestinationAddress.Should().Be(destinationAddress);
        ctx.SentTime.Should().Be(sentTime);
        ctx.ContentType.Should().Be(contentType);
        ctx.Headers["x-custom"].Should().Be("value");
    }

    [Fact]
    public void Headers_WhenEmpty_ReturnsEmptyDictionary()
    {
        var ctx = CreateContext(headers: new Dictionary<string, string>());

        ctx.Headers.Should().NotBeNull();
        ctx.Headers.Count.Should().Be(0);
    }

    [Fact]
    public void CorrelationId_WhenNull_ReturnsNull()
    {
        var ctx = CreateContext(correlationId: null);

        ctx.CorrelationId.Should().BeNull();
    }

    [Fact]
    public void CancellationToken_PropagatesFromConstructor()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var ctx = CreateContext(cancellationToken: cts.Token);

        ctx.CancellationToken.IsCancellationRequested.Should().BeTrue();
    }
}

/// <summary>
/// Tests for the base <see cref="ConsumeContext.RespondAsync{T}"/> implementation,
/// covering the request-response routing path (ReplyTo header → queue: URI) and the
/// fallback to <see cref="ConsumeContext.PublishAsync{T}"/>.
/// </summary>
public sealed class ConsumeContextRespondAsyncTests
{
    private sealed record ResponseMsg(string Value);

    private static RespondTestableConsumeContext CreateContext(
        IReadOnlyDictionary<string, string> headers,
        IPublishEndpoint? publishEndpoint = null,
        ISendEndpointProvider? sendEndpointProvider = null)
    {
        return new RespondTestableConsumeContext(
            Guid.NewGuid(),
            correlationId: null,
            conversationId: null,
            sourceAddress: null,
            destinationAddress: null,
            sentTime: null,
            headers: headers,
            contentType: null,
            rawBody: default,
            publishEndpoint: publishEndpoint ?? Substitute.For<IPublishEndpoint>(),
            sendEndpointProvider: sendEndpointProvider ?? Substitute.For<ISendEndpointProvider>());
    }

    [Fact]
    public async Task RespondAsync_WithReplyToHeader_CallsGetSendEndpointWithQueueUri()
    {
        // Arrange
        var headers = new Dictionary<string, string>
        {
            ["ReplyTo"] = "amq.gen-test-reply-queue",
            ["correlation-id"] = "abc-123",
        };

        ISendEndpoint sendEndpoint = Substitute.For<ISendEndpoint>();
        sendEndpoint.SendAsync(Arg.Any<ResponseMsg>(), Arg.Any<CancellationToken>())
                    .Returns(Task.CompletedTask);

        ISendEndpointProvider sendEndpointProvider = Substitute.For<ISendEndpointProvider>();
        sendEndpointProvider
            .GetSendEndpoint(Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(sendEndpoint));

        IPublishEndpoint publishEndpoint = Substitute.For<IPublishEndpoint>();

        var ctx = CreateContext(headers, publishEndpoint, sendEndpointProvider);
        var response = new ResponseMsg("ok");

        // Act
        await ctx.RespondAsync(response, CancellationToken.None);

        // Assert — GetSendEndpoint must be called with a queue: URI, not PublishAsync.
        await sendEndpointProvider.Received(1)
            .GetSendEndpoint(
                Arg.Is<Uri>(u => u.Scheme == "queue"),
                Arg.Any<CancellationToken>());
        await sendEndpoint.Received(1).SendAsync(response, Arg.Any<CancellationToken>());
        await publishEndpoint.DidNotReceive().PublishAsync(Arg.Any<ResponseMsg>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RespondAsync_WithReplyToHeader_EncodesCorrelationIdInQueueUri()
    {
        // Arrange
        const string correlationId = "f47ac10b-58cc-4372-a567-0e02b2c3d479";
        var headers = new Dictionary<string, string>
        {
            ["ReplyTo"] = "reply-queue",
            ["correlation-id"] = correlationId,
        };

        Uri? capturedUri = null;
        ISendEndpoint sendEndpoint = Substitute.For<ISendEndpoint>();
        sendEndpoint.SendAsync(Arg.Any<ResponseMsg>(), Arg.Any<CancellationToken>())
                    .Returns(Task.CompletedTask);

        ISendEndpointProvider provider = Substitute.For<ISendEndpointProvider>();
        provider.GetSendEndpoint(Arg.Any<Uri>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    capturedUri = callInfo.ArgAt<Uri>(0);
                    return Task.FromResult(sendEndpoint);
                });

        var ctx = CreateContext(headers, sendEndpointProvider: provider);

        // Act
        await ctx.RespondAsync(new ResponseMsg("result"), CancellationToken.None);

        // Assert — the URI must contain the correlation-id query parameter.
        capturedUri.Should().NotBeNull();
        capturedUri!.Query.Should().Contain($"correlation-id={Uri.EscapeDataString(correlationId)}");
    }

    [Fact]
    public async Task RespondAsync_WithoutReplyToHeader_FallsBackToPublishAsync()
    {
        // Arrange — no ReplyTo header present.
        var headers = new Dictionary<string, string>();

        IPublishEndpoint publishEndpoint = Substitute.For<IPublishEndpoint>();
        publishEndpoint.PublishAsync(Arg.Any<ResponseMsg>(), Arg.Any<CancellationToken>())
                       .Returns(Task.CompletedTask);

        ISendEndpointProvider sendEndpointProvider = Substitute.For<ISendEndpointProvider>();

        var ctx = CreateContext(headers, publishEndpoint, sendEndpointProvider);
        var response = new ResponseMsg("broadcast");

        // Act
        await ctx.RespondAsync(response, CancellationToken.None);

        // Assert — must fall back to PublishAsync, never GetSendEndpoint.
        await publishEndpoint.Received(1).PublishAsync(response, Arg.Any<CancellationToken>());
        await sendEndpointProvider.DidNotReceive()
            .GetSendEndpoint(Arg.Any<Uri>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RespondAsync_WithEmptyReplyToHeader_FallsBackToPublishAsync()
    {
        // Arrange — ReplyTo is present but empty.
        var headers = new Dictionary<string, string> { ["ReplyTo"] = "" };

        IPublishEndpoint publishEndpoint = Substitute.For<IPublishEndpoint>();
        publishEndpoint.PublishAsync(Arg.Any<ResponseMsg>(), Arg.Any<CancellationToken>())
                       .Returns(Task.CompletedTask);

        ISendEndpointProvider sendEndpointProvider = Substitute.For<ISendEndpointProvider>();

        var ctx = CreateContext(headers, publishEndpoint, sendEndpointProvider);

        // Act
        await ctx.RespondAsync(new ResponseMsg("broadcast"), CancellationToken.None);

        // Assert — empty ReplyTo must also fall back to PublishAsync.
        await publishEndpoint.Received(1)
            .PublishAsync(Arg.Any<ResponseMsg>(), Arg.Any<CancellationToken>());
        await sendEndpointProvider.DidNotReceive()
            .GetSendEndpoint(Arg.Any<Uri>(), Arg.Any<CancellationToken>());
    }
}
