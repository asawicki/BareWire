using System.Buffers;
using System.Text;
using AwesomeAssertions;
using NSubstitute;
using BareWire.Abstractions;
using BareWire.Abstractions.Serialization;

namespace BareWire.UnitTests.Abstractions;

public sealed class RawConsumeContextTests
{
    private sealed record SampleMessage(string Name);

    private static RawConsumeContext CreateContext(
        ReadOnlySequence<byte> rawBody = default,
        IMessageDeserializer? deserializer = null)
    {
        return new RawConsumeContext(
            messageId: Guid.NewGuid(),
            correlationId: null,
            conversationId: null,
            sourceAddress: null,
            destinationAddress: null,
            sentTime: null,
            headers: new Dictionary<string, string>(),
            contentType: "application/json",
            rawBody: rawBody,
            publishEndpoint: Substitute.For<IPublishEndpoint>(),
            sendEndpointProvider: Substitute.For<ISendEndpointProvider>(),
            deserializer: deserializer ?? Substitute.For<IMessageDeserializer>());
    }

    [Fact]
    public void TryDeserialize_WithValidJson_ReturnsObject()
    {
        var expected = new SampleMessage("Alice");
        var bodyBytes = Encoding.UTF8.GetBytes("""{"Name":"Alice"}""");
        var rawBody = new ReadOnlySequence<byte>(bodyBytes);

        var deserializer = Substitute.For<IMessageDeserializer>();
        deserializer.Deserialize<SampleMessage>(Arg.Any<ReadOnlySequence<byte>>()).Returns(expected);

        var ctx = CreateContext(rawBody: rawBody, deserializer: deserializer);

        var result = ctx.TryDeserialize<SampleMessage>();

        result.Should().NotBeNull();
        result!.Name.Should().Be("Alice");
    }

    [Fact]
    public void TryDeserialize_WithInvalidJson_ReturnsNull()
    {
        var bodyBytes = Encoding.UTF8.GetBytes("not-valid-json");
        var rawBody = new ReadOnlySequence<byte>(bodyBytes);

        var deserializer = Substitute.For<IMessageDeserializer>();
        deserializer.Deserialize<SampleMessage>(Arg.Any<ReadOnlySequence<byte>>())
            .Returns(_ => throw new InvalidOperationException("parse failure"));

        var ctx = CreateContext(rawBody: rawBody, deserializer: deserializer);

        var result = ctx.TryDeserialize<SampleMessage>();

        result.Should().BeNull();
    }

    [Fact]
    public void TryDeserialize_WithEmptyBody_ReturnsNull()
    {
        var ctx = CreateContext(rawBody: ReadOnlySequence<byte>.Empty);

        var result = ctx.TryDeserialize<SampleMessage>();

        result.Should().BeNull();
    }
}
