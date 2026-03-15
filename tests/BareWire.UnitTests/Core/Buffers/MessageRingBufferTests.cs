using AwesomeAssertions;
using BareWire.Core.Buffers;

namespace BareWire.UnitTests.Core.Buffers;

public sealed class MessageRingBufferTests
{
    [Fact]
    public void TryWrite_WhenEmpty_ReturnsTrue()
    {
        var buffer = new MessageRingBuffer<int>(4);

        bool result = buffer.TryWrite(42);

        result.Should().BeTrue();
    }

    [Fact]
    public void TryRead_WhenEmpty_ReturnsFalse()
    {
        var buffer = new MessageRingBuffer<int>(4);

        bool result = buffer.TryRead(out int item);

        result.Should().BeFalse();
        item.Should().Be(default);
    }

    [Fact]
    public void TryWrite_WhenFull_ReturnsFalse()
    {
        var buffer = new MessageRingBuffer<int>(4);

        for (int i = 0; i < 4; i++)
            buffer.TryWrite(i).Should().BeTrue();

        bool result = buffer.TryWrite(99);

        result.Should().BeFalse();
    }

    [Fact]
    public void TryRead_AfterWrite_ReturnsWrittenItem()
    {
        var buffer = new MessageRingBuffer<string>(4);
        buffer.TryWrite("hello");

        bool result = buffer.TryRead(out string? item);

        result.Should().BeTrue();
        item.Should().Be("hello");
    }

    [Fact]
    public void Count_AfterMultipleWrites_ReturnsCorrectCount()
    {
        var buffer = new MessageRingBuffer<int>(8);

        buffer.TryWrite(1);
        buffer.TryWrite(2);
        buffer.TryWrite(3);

        buffer.Count.Should().Be(3);
    }

    [Fact]
    public void Constructor_NonPowerOfTwo_RoundsUpToNextPowerOfTwo()
    {
        // 5 should round up to 8
        var buffer = new MessageRingBuffer<int>(5);

        buffer.Capacity.Should().Be(8);
    }

    [Fact]
    public void WrapAround_AfterManyWritesAndReads_WorksCorrectly()
    {
        // Use capacity 4 so wrap-around happens quickly.
        var buffer = new MessageRingBuffer<int>(4);

        // Fill and drain twice to exercise the wrap-around path.
        for (int round = 0; round < 2; round++)
        {
            for (int i = 0; i < 4; i++)
                buffer.TryWrite(i * 10).Should().BeTrue();

            for (int i = 0; i < 4; i++)
            {
                buffer.TryRead(out int item).Should().BeTrue();
                item.Should().Be(i * 10);
            }
        }

        buffer.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void TryWrite_AfterOverflow_ReturnsFalse()
    {
        var buffer = new MessageRingBuffer<int>(4);

        // Fill to capacity.
        for (int i = 0; i < 4; i++)
            buffer.TryWrite(i).Should().BeTrue();

        // Multiple writes beyond capacity must all return false and not corrupt state.
        buffer.TryWrite(100).Should().BeFalse();
        buffer.TryWrite(101).Should().BeFalse();
        buffer.TryWrite(102).Should().BeFalse();

        // Buffer still has exactly the original 4 items.
        buffer.Count.Should().Be(4);

        // Drain and verify integrity.
        for (int i = 0; i < 4; i++)
        {
            buffer.TryRead(out int item).Should().BeTrue();
            item.Should().Be(i);
        }

        buffer.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task TryRead_ConcurrentSPSC_AllItemsDelivered()
    {
        const int itemCount = 10_000;
        var buffer = new MessageRingBuffer<int>(1024);
        var received = new List<int>(itemCount);

        Task writer = Task.Run(() =>
        {
            for (int i = 0; i < itemCount; i++)
            {
                // Spin until the write succeeds (back-pressure when buffer is full).
                while (!buffer.TryWrite(i))
                    Thread.SpinWait(1);
            }
        });

        Task reader = Task.Run(() =>
        {
            int readCount = 0;
            while (readCount < itemCount)
            {
                if (buffer.TryRead(out int item))
                {
                    received.Add(item);
                    readCount++;
                }
                else
                {
                    Thread.SpinWait(1);
                }
            }
        });

        await Task.WhenAll(writer, reader);

        received.Should().HaveCount(itemCount);
        received.Should().Equal(Enumerable.Range(0, itemCount));
    }

    [Fact]
    public void Constructor_ZeroCapacity_ThrowsArgumentOutOfRangeException()
    {
        Action act = () => _ = new MessageRingBuffer<int>(0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_NegativeCapacity_ThrowsArgumentOutOfRangeException()
    {
        Action act = () => _ = new MessageRingBuffer<int>(-1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
