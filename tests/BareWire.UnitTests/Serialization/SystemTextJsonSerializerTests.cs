using System.Buffers;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using BareWire.Abstractions.Exceptions;
using BareWire.Core.Buffers;
using BareWire.Serialization.Json;

namespace BareWire.UnitTests.Serialization;

public sealed class SystemTextJsonSerializerTests
{
    private readonly SystemTextJsonSerializer _sut = new();

    [Fact]
    public void Serialize_SimpleRecord_ProducesValidJson()
    {
        using var writer = new PooledBufferWriter();
        var message = new SimpleMessage("hello", 42);

        _sut.Serialize(message, writer);

        var json = Encoding.UTF8.GetString(writer.WrittenSpan);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("name").GetString().Should().Be("hello");
        doc.RootElement.GetProperty("value").GetInt32().Should().Be(42);
    }

    [Fact]
    public void Serialize_NestedObject_ProducesValidJson()
    {
        using var writer = new PooledBufferWriter();
        var message = new NestedMessage("outer", new InnerData(7, "inner-desc"));

        _sut.Serialize(message, writer);

        var json = Encoding.UTF8.GetString(writer.WrittenSpan);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("name").GetString().Should().Be("outer");
        doc.RootElement.GetProperty("inner").GetProperty("id").GetInt32().Should().Be(7);
        doc.RootElement.GetProperty("inner").GetProperty("description").GetString().Should().Be("inner-desc");
    }

    [Fact]
    public void Serialize_RecordWithCollections_ProducesValidJson()
    {
        using var writer = new PooledBufferWriter();
        var message = new CollectionMessage(
            "tagged",
            ["a", "b", "c"],
            new Dictionary<string, int> { ["x"] = 1, ["y"] = 2 });

        _sut.Serialize(message, writer);

        var json = Encoding.UTF8.GetString(writer.WrittenSpan);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("name").GetString().Should().Be("tagged");

        var tags = doc.RootElement.GetProperty("tags");
        tags.GetArrayLength().Should().Be(3);

        var scores = doc.RootElement.GetProperty("scores");
        scores.GetProperty("x").GetInt32().Should().Be(1);
        scores.GetProperty("y").GetInt32().Should().Be(2);
    }

    [Fact]
    public void Serialize_NullMessage_ThrowsArgumentNullException()
    {
        using var writer = new PooledBufferWriter();
        SimpleMessage message = null!;

        Action act = () => _sut.Serialize(message, writer);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("message");
    }

    [Fact]
    public void Serialize_NullOutput_ThrowsArgumentNullException()
    {
        IBufferWriter<byte> output = null!;
        var message = new SimpleMessage("x", 1);

        Action act = () => _sut.Serialize(message, output);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("output");
    }

    [Fact]
    public void ContentType_ReturnsApplicationJson()
    {
        _sut.ContentType.Should().Be("application/json");
    }
}

internal sealed record SimpleMessage(string Name, int Value);
internal sealed record NestedMessage(string Name, InnerData Inner);
internal sealed record InnerData(int Id, string Description);
internal sealed record CollectionMessage(string Name, List<string> Tags, Dictionary<string, int> Scores);
