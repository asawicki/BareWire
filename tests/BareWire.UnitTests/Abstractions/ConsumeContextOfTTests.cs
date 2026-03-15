using System.Buffers;
using AwesomeAssertions;
using NSubstitute;
using BareWire.Abstractions;

namespace BareWire.UnitTests.Abstractions;

public sealed class ConsumeContextOfTTests
{
    private sealed record TestMessage(string Value);

    private static ConsumeContext<TestMessage> CreateContext(
        TestMessage? message = null,
        Guid? messageId = null,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        return new ConsumeContext<TestMessage>(
            message ?? new TestMessage("hello"),
            messageId ?? Guid.NewGuid(),
            correlationId: null,
            conversationId: null,
            sourceAddress: null,
            destinationAddress: null,
            sentTime: null,
            headers: headers ?? new Dictionary<string, string>(),
            contentType: "application/json",
            rawBody: default,
            publishEndpoint: Substitute.For<IPublishEndpoint>(),
            sendEndpointProvider: Substitute.For<ISendEndpointProvider>());
    }

    [Fact]
    public void Message_ReturnsDeserializedPayload()
    {
        var expected = new TestMessage("payload-value");

        var ctx = CreateContext(message: expected);

        ctx.Message.Should().Be(expected);
        ctx.Message.Value.Should().Be("payload-value");
    }

    [Fact]
    public void Message_InheritsBaseContextProperties()
    {
        var messageId = Guid.NewGuid();
        var headers = new Dictionary<string, string> { ["x-trace"] = "abc123" };

        var ctx = CreateContext(messageId: messageId, headers: headers);

        ctx.MessageId.Should().Be(messageId);
        ctx.Headers["x-trace"].Should().Be("abc123");
    }
}
