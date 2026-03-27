using System.Buffers;
using AwesomeAssertions;
using BareWire.Buffers;

namespace BareWire.UnitTests.Core.Buffers;

public sealed class PooledBufferWriterTests
{
    [Fact]
    public void GetSpan_ReturnsNonEmptySpan()
    {
        using var writer = new PooledBufferWriter();

        Span<byte> span = writer.GetSpan(16);

        span.IsEmpty.Should().BeFalse();
        span.Length.Should().BeGreaterThanOrEqualTo(16);
    }

    [Fact]
    public void Advance_UpdatesWrittenCount()
    {
        using var writer = new PooledBufferWriter();
        writer.GetSpan(8); // ensure capacity

        writer.Advance(5);

        writer.WrittenCount.Should().Be(5);
    }

    [Fact]
    public void GetMemory_WhenNeedsGrow_ResizesBuffer()
    {
        // Start with a tiny buffer so a grow is forced.
        using var writer = new PooledBufferWriter(initialCapacity: 4);

        // Request more space than the initial capacity.
        Memory<byte> memory = writer.GetMemory(256);

        memory.Length.Should().BeGreaterThanOrEqualTo(256);
    }

    [Fact]
    public void WrittenMemory_ReturnsCorrectData()
    {
        using var writer = new PooledBufferWriter();

        Span<byte> span = writer.GetSpan(3);
        span[0] = 0xAA;
        span[1] = 0xBB;
        span[2] = 0xCC;
        writer.Advance(3);

        ReadOnlyMemory<byte> written = writer.WrittenMemory;

        written.Length.Should().Be(3);
        written.Span[0].Should().Be(0xAA);
        written.Span[1].Should().Be(0xBB);
        written.Span[2].Should().Be(0xCC);
    }

    [Fact]
    public void Dispose_ReturnsBufferToPool()
    {
        // Verifies that Dispose does not throw and completes without leaking.
        // ArrayPool doesn't expose a way to verify a specific buffer was returned,
        // so we assert that disposing twice is safe (idempotent).
        var writer = new PooledBufferWriter();

        writer.Dispose();

        var act = () => writer.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void GetSpan_AfterDispose_ThrowsObjectDisposedException()
    {
        var writer = new PooledBufferWriter();
        writer.Dispose();

        // GetSpan returns Span<byte> which is a ref struct, so we use GetMemory instead
        // (same dispose guard is hit) to keep the lambda capturable.
        Action act = () => writer.GetMemory(1);

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Reset_SetsWrittenCountToZero()
    {
        using var writer = new PooledBufferWriter();
        writer.GetSpan(4);
        writer.Advance(4);

        writer.Reset();

        writer.WrittenCount.Should().Be(0);
    }

    [Fact]
    public void Advance_BeyondBuffer_ThrowsArgumentOutOfRangeException()
    {
        using var writer = new PooledBufferWriter();

        // Request a span to find out how many bytes are actually available,
        // then advance exactly to the end so no bytes remain.
        Span<byte> available = writer.GetSpan(0);
        writer.Advance(available.Length);

        // Now the buffer is full; any further advance must throw.
        Action act = () => writer.Advance(1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GetMemory_AfterDispose_ThrowsObjectDisposedException()
    {
        var writer = new PooledBufferWriter();
        writer.Dispose();

        Action act = () => writer.GetMemory(1);

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void WrittenSpan_ReturnsCorrectSlice()
    {
        using var writer = new PooledBufferWriter();

        Span<byte> span = writer.GetSpan(4);
        span[0] = 0x01;
        span[1] = 0x02;
        span[2] = 0x03;
        span[3] = 0x04;
        writer.Advance(4);

        ReadOnlySpan<byte> writtenSpan = writer.WrittenSpan;

        writtenSpan.Length.Should().Be(4);
        writtenSpan[0].Should().Be(0x01);
        writtenSpan[1].Should().Be(0x02);
        writtenSpan[2].Should().Be(0x03);
        writtenSpan[3].Should().Be(0x04);
    }

    [Fact]
    public void Reset_AfterWrites_ClearsWrittenCount()
    {
        using var writer = new PooledBufferWriter();

        // First write.
        writer.GetSpan(3);
        writer.Advance(3);
        writer.WrittenCount.Should().Be(3);

        writer.Reset();

        // After reset the count is zero and we can write again.
        writer.GetSpan(2);
        writer.Advance(2);
        writer.WrittenCount.Should().Be(2);
    }

    [Fact]
    public void GetMemory_LargeSize_GrowsBuffer()
    {
        // Start with a small initial capacity.
        using var writer = new PooledBufferWriter(initialCapacity: 4);

        // Requesting a size much larger than the initial capacity must succeed after grow.
        Memory<byte> memory = writer.GetMemory(4096);

        memory.Length.Should().BeGreaterThanOrEqualTo(4096);
    }
}
