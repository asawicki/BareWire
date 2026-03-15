using System.Buffers;
using AwesomeAssertions;
using NSubstitute;
using BareWire.Abstractions;

namespace BareWire.UnitTests.Abstractions;

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
