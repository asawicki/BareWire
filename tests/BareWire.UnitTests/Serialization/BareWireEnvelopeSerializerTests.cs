using System.Buffers;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using BareWire.Core.Buffers;
using BareWire.Serialization.Json;

namespace BareWire.UnitTests.Serialization;

public sealed class BareWireEnvelopeSerializerTests
{
    private readonly BareWireEnvelopeSerializer _sut = new();

    [Fact]
    public void Serialize_WithEnvelope_ProducesEnvelopeJson()
    {
        using var writer = new PooledBufferWriter();
        var message = new SimpleMessage("env-test", 7);

        _sut.Serialize(message, writer);

        var json = Encoding.UTF8.GetString(writer.WrittenSpan);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Envelope must contain top-level metadata fields
        root.TryGetProperty("messageId", out _).Should().BeTrue();
        root.TryGetProperty("sentTime", out _).Should().BeTrue();
        root.TryGetProperty("messageType", out _).Should().BeTrue();
        root.TryGetProperty("body", out var body).Should().BeTrue();

        // Body must carry the original message payload
        body.GetProperty("name").GetString().Should().Be("env-test");
        body.GetProperty("value").GetInt32().Should().Be(7);
    }

    [Fact]
    public void Deserialize_EnvelopeJson_ExtractsBodyAndMetadata()
    {
        using var writer = new PooledBufferWriter();
        var original = new SimpleMessage("roundtrip", 123);
        _sut.Serialize(original, writer);
        var sequence = new ReadOnlySequence<byte>(writer.WrittenMemory.ToArray());

        var result = _sut.Deserialize<SimpleMessage>(sequence);

        result.Should().NotBeNull();
        result!.Name.Should().Be("roundtrip");
        result.Value.Should().Be(123);
    }

    [Fact]
    public void Roundtrip_PreservesAllMetadata()
    {
        using var writer = new PooledBufferWriter();
        var message = new SimpleMessage("meta-check", 0);

        _sut.Serialize(message, writer);

        var json = Encoding.UTF8.GetString(writer.WrittenSpan);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // MessageId must be a non-empty GUID
        root.GetProperty("messageId").GetGuid().Should().NotBeEmpty();

        // SentTime must be a valid DateTimeOffset
        root.GetProperty("sentTime").GetDateTimeOffset().Should().NotBe(default);

        // MessageType must be a non-empty array containing a URN
        var msgType = root.GetProperty("messageType");
        msgType.GetArrayLength().Should().BeGreaterThan(0);
        msgType[0].GetString().Should().StartWith("urn:message:");
        msgType[0].GetString().Should().Contain(nameof(SimpleMessage));

        // CorrelationId and ConversationId are optional — they may be absent or null
        // (the serializer uses WhenWritingNull so they are omitted from output)
        root.TryGetProperty("correlationId", out _).Should().BeFalse();
        root.TryGetProperty("conversationId", out _).Should().BeFalse();
    }

    [Fact]
    public void ContentType_ReturnsVndBarewireJson()
    {
        _sut.ContentType.Should().Be("application/vnd.barewire+json");
    }
}
