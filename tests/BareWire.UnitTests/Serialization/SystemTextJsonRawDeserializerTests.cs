using System.Buffers;
using System.Text;
using AwesomeAssertions;
using BareWire.Abstractions.Exceptions;
using BareWire.Core.Buffers;
using BareWire.Serialization.Json;

namespace BareWire.UnitTests.Serialization;

public sealed class SystemTextJsonRawDeserializerTests
{
    private readonly SystemTextJsonSerializer _serializer = new();
    private readonly SystemTextJsonRawDeserializer _sut = new();

    [Fact]
    public void Deserialize_SimpleRecord_ReturnsInstance()
    {
        var original = new SimpleMessage("hello", 42);
        using var writer = new PooledBufferWriter();
        _serializer.Serialize(original, writer);
        var sequence = new ReadOnlySequence<byte>(writer.WrittenMemory.ToArray());

        var result = _sut.Deserialize<SimpleMessage>(sequence);

        result.Should().NotBeNull();
        result!.Name.Should().Be("hello");
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Deserialize_NestedObject_ReturnsInstance()
    {
        var original = new NestedMessage("outer", new InnerData(7, "desc"));
        using var writer = new PooledBufferWriter();
        _serializer.Serialize(original, writer);
        var sequence = new ReadOnlySequence<byte>(writer.WrittenMemory.ToArray());

        var result = _sut.Deserialize<NestedMessage>(sequence);

        result.Should().NotBeNull();
        result!.Name.Should().Be("outer");
        result.Inner.Id.Should().Be(7);
        result.Inner.Description.Should().Be("desc");
    }

    [Fact]
    public void Deserialize_EmptyPayload_ReturnsNull()
    {
        var empty = ReadOnlySequence<byte>.Empty;

        var result = _sut.Deserialize<SimpleMessage>(empty);

        result.Should().BeNull();
    }

    [Fact]
    public void Deserialize_InvalidJson_ThrowsBareWireSerializationException()
    {
        var badJson = Encoding.UTF8.GetBytes("not valid json {{{{");
        var sequence = new ReadOnlySequence<byte>(badJson);

        Action act = () => _sut.Deserialize<SimpleMessage>(sequence);

        act.Should().Throw<BareWireSerializationException>();
    }

    [Fact]
    public void Deserialize_NullProperties_HandlesGracefully()
    {
        var json = Encoding.UTF8.GetBytes("""{"name":null,"value":0}""");
        var sequence = new ReadOnlySequence<byte>(json);

        var result = _sut.Deserialize<SimpleMessage>(sequence);

        result.Should().NotBeNull();
        result!.Name.Should().BeNull();
        result.Value.Should().Be(0);
    }

    [Fact]
    public void Deserialize_MultiSegmentSequence_ReturnsInstance()
    {
        var original = new SimpleMessage("multi", 99);
        using var writer = new PooledBufferWriter();
        _serializer.Serialize(original, writer);
        byte[] bytes = writer.WrittenMemory.ToArray();
        var sequence = CreateMultiSegmentSequence(bytes, segmentSize: 4);

        var result = _sut.Deserialize<SimpleMessage>(sequence);

        result.Should().NotBeNull();
        result!.Name.Should().Be("multi");
        result.Value.Should().Be(99);
    }

    [Fact]
    public void Deserialize_LargePayload_Over64KB_Succeeds()
    {
        // Build a record with a string that pushes the payload well past 64 KB
        var largeString = new string('A', 70_000);
        var original = new SimpleMessage(largeString, 1);
        using var writer = new PooledBufferWriter(initialCapacity: 80_000);
        _serializer.Serialize(original, writer);
        var sequence = new ReadOnlySequence<byte>(writer.WrittenMemory.ToArray());

        var result = _sut.Deserialize<SimpleMessage>(sequence);

        result.Should().NotBeNull();
        result!.Name.Should().Be(largeString);
        result.Value.Should().Be(1);
    }

    [Fact]
    public void Deserialize_RecordTypes_Roundtrip()
    {
        var original = new CollectionMessage(
            "tagged",
            ["x", "y"],
            new Dictionary<string, int> { ["score"] = 100 });
        using var writer = new PooledBufferWriter();
        _serializer.Serialize(original, writer);
        var sequence = new ReadOnlySequence<byte>(writer.WrittenMemory.ToArray());

        var result = _sut.Deserialize<CollectionMessage>(sequence);

        result.Should().NotBeNull();
        result!.Name.Should().Be("tagged");
        result.Tags.Should().BeEquivalentTo(["x", "y"]);
        result.Scores["score"].Should().Be(100);
    }

    [Fact]
    public void ContentType_ReturnsApplicationJson()
    {
        _sut.ContentType.Should().Be("application/json");
    }

    // --- Multi-segment helper ---

    private static ReadOnlySequence<byte> CreateMultiSegmentSequence(byte[] data, int segmentSize)
    {
        if (data.Length <= segmentSize)
            return new ReadOnlySequence<byte>(data);

        var segments = new List<TestSegment>();
        for (int offset = 0; offset < data.Length; offset += segmentSize)
        {
            int length = Math.Min(segmentSize, data.Length - offset);
            segments.Add(new TestSegment(data.AsMemory(offset, length)));
        }

        for (int i = 1; i < segments.Count; i++)
            segments[i - 1].SetNext(segments[i]);

        return new ReadOnlySequence<byte>(
            segments[0], 0,
            segments[^1], segments[^1].Memory.Length);
    }

    private sealed class TestSegment : ReadOnlySequenceSegment<byte>
    {
        public TestSegment(ReadOnlyMemory<byte> memory) => Memory = memory;

        public void SetNext(TestSegment next)
        {
            next.RunningIndex = RunningIndex + Memory.Length;
            Next = next;
        }
    }
}
