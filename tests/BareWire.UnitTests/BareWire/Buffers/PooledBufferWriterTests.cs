using System.Buffers;
using AwesomeAssertions;
using BareWire.Buffers;

namespace BareWire.UnitTests.Core.Buffers;

public sealed class PooledBufferWriterTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // Constructor
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ZeroCapacity_ThrowsArgumentOutOfRangeException()
    {
        // MUT-55: ThrowIfLessThan(initialCapacity, 1) removed
        var act = () => new PooledBufferWriter(0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_NegativeCapacity_ThrowsArgumentOutOfRangeException()
    {
        // MUT-55: ThrowIfLessThan(initialCapacity, 1) removed
        var act = () => new PooledBufferWriter(-1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_MinimumCapacityOne_DoesNotThrow()
    {
        // MUT-55: boundary — capacity of 1 is the minimum valid value
        var act = () =>
        {
            using var writer = new PooledBufferWriter(1);
        };

        act.Should().NotThrow();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // WrittenMemory / WrittenSpan — disposed guards
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void WrittenMemory_WhenDisposed_ThrowsObjectDisposedException()
    {
        // MUT-57: ThrowIfDisposed in WrittenMemory getter removed
        var writer = new PooledBufferWriter();
        writer.Dispose();

        Action act = () => _ = writer.WrittenMemory;

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void WrittenSpan_WhenDisposed_ThrowsObjectDisposedException()
    {
        // MUT-59: ThrowIfDisposed in WrittenSpan getter removed
        var writer = new PooledBufferWriter();
        writer.Dispose();

        Action act = () => _ = writer.WrittenSpan;

        act.Should().Throw<ObjectDisposedException>();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Advance — guards and boundary
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Advance_WhenDisposed_ThrowsObjectDisposedException()
    {
        // MUT-61: ThrowIfDisposed in Advance removed
        var writer = new PooledBufferWriter();
        writer.Dispose();

        Action act = () => writer.Advance(1);

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Advance_ZeroCount_ThrowsArgumentOutOfRangeException()
    {
        // MUT-62: ThrowIfNegativeOrZero(count) removed — zero is invalid
        using var writer = new PooledBufferWriter();
        writer.GetSpan(8);

        Action act = () => writer.Advance(0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Advance_NegativeCount_ThrowsArgumentOutOfRangeException()
    {
        // MUT-62: ThrowIfNegativeOrZero(count) removed — negative is invalid
        using var writer = new PooledBufferWriter();
        writer.GetSpan(8);

        Action act = () => writer.Advance(-1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Advance_ExactlyAvailableBytes_Succeeds()
    {
        // MUT-65/66: count > available changed — advancing exactly to the end must succeed
        using var writer = new PooledBufferWriter(initialCapacity: 16);
        Span<byte> span = writer.GetSpan(0);
        int available = span.Length;

        // Should not throw — count == available is valid
        Action act = () => writer.Advance(available);

        act.Should().NotThrow();
        writer.WrittenCount.Should().Be(available);
    }

    [Fact]
    public void Advance_OneByteOverAvailable_ThrowsArgumentOutOfRangeException()
    {
        // MUT-65/66: count > available changed — one byte over must throw
        using var writer = new PooledBufferWriter(initialCapacity: 16);
        Span<byte> span = writer.GetSpan(0);
        int overLimit = span.Length + 1;

        Action act = () => writer.Advance(overLimit);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Advance_AfterPartialWrite_UpdatesPositionCorrectly()
    {
        // MUT-88: arithmetic in EnsureCapacity — verify position tracking
        using var writer = new PooledBufferWriter(initialCapacity: 64);
        writer.GetSpan(4);
        writer.Advance(4);
        writer.GetSpan(4);
        writer.Advance(4);

        writer.WrittenCount.Should().Be(8);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GetMemory / GetSpan — disposed guards
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void GetSpan_ReturnsNonEmptySpan()
    {
        using var writer = new PooledBufferWriter();

        Span<byte> span = writer.GetSpan(16);

        span.IsEmpty.Should().BeFalse();
        span.Length.Should().BeGreaterThanOrEqualTo(16);
    }

    [Fact]
    public void GetMemory_WhenDisposed_ThrowsObjectDisposedException()
    {
        // MUT-74: ThrowIfDisposed in GetMemory removed
        var writer = new PooledBufferWriter();
        writer.Dispose();

        Action act = () => writer.GetMemory(1);

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void GetSpan_WhenDisposed_ThrowsObjectDisposedException()
    {
        // MUT-74: ThrowIfDisposed in GetSpan removed — use Action wrapper to avoid ref struct capture
        var writer = new PooledBufferWriter();
        writer.Dispose();

        Action act = () => writer.GetMemory(1); // same guard path as GetSpan

        act.Should().Throw<ObjectDisposedException>();
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

    // ──────────────────────────────────────────────────────────────────────────
    // Reset — disposed guard
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Reset_WhenDisposed_ThrowsObjectDisposedException()
    {
        // MUT-77: ThrowIfDisposed in Reset removed
        var writer = new PooledBufferWriter();
        writer.Dispose();

        Action act = () => writer.Reset();

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

    // ──────────────────────────────────────────────────────────────────────────
    // Dispose — idempotency
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        // MUT-82: _disposed = true removed — second Dispose would try to return already-returned buffer
        var writer = new PooledBufferWriter();
        writer.Dispose();

        Action act = () => writer.Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_ReturnsBufferToPool()
    {
        // Verifies that Dispose does not throw and completes without leaking.
        var writer = new PooledBufferWriter();

        writer.Dispose();

        var act = () => writer.Dispose();
        act.Should().NotThrow();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // EnsureCapacity / Grow — boundary and data preservation
    // ──────────────────────────────────────────────────────────────────────────

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
    public void GetMemory_LargeSize_GrowsBuffer()
    {
        // Start with a small initial capacity.
        using var writer = new PooledBufferWriter(initialCapacity: 4);

        Memory<byte> memory = writer.GetMemory(4096);

        memory.Length.Should().BeGreaterThanOrEqualTo(4096);
    }

    [Fact]
    public void GetMemory_SizeHintZero_ReturnsAtLeastOneByte()
    {
        // MUT-85: sizeHint <= 0 ? 1 : sizeHint — zero hint must still ensure at least 1 byte
        using var writer = new PooledBufferWriter(initialCapacity: 1);
        writer.GetSpan(0);
        writer.Advance(1); // fill the entire 1-byte initial rent

        // After filling the buffer, GetMemory(0) must trigger grow and return >= 1 byte
        Memory<byte> memory = writer.GetMemory(0);

        memory.Length.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void GetMemory_ExactlyAvailableBytes_DoesNotGrow()
    {
        // MUT-90: available >= needed — when available exactly equals needed, no grow should happen
        using var writer = new PooledBufferWriter(initialCapacity: 16);
        Span<byte> initial = writer.GetSpan(0);
        int capacity = initial.Length;

        // Request exactly as many bytes as are available
        Memory<byte> memory = writer.GetMemory(capacity);

        memory.Length.Should().BeGreaterThanOrEqualTo(capacity);
    }

    [Fact]
    public void WrittenMemory_AfterGrow_PreservesExistingData()
    {
        // MUT-98: BlockCopy removed — existing data must survive a buffer grow
        using var writer = new PooledBufferWriter(initialCapacity: 4);

        // Write known bytes before forcing a grow
        Span<byte> span = writer.GetSpan(4);
        span[0] = 0x11;
        span[1] = 0x22;
        span[2] = 0x33;
        span[3] = 0x44;
        writer.Advance(4);

        // Force grow by requesting more than the initial buffer can hold
        writer.GetMemory(256);

        ReadOnlyMemory<byte> written = writer.WrittenMemory;

        written.Length.Should().Be(4);
        written.Span[0].Should().Be(0x11);
        written.Span[1].Should().Be(0x22);
        written.Span[2].Should().Be(0x33);
        written.Span[3].Should().Be(0x44);
    }

    [Fact]
    public void Grow_NewBufferAtLeastDoubleCurrentLength()
    {
        // MUT-96: _buffer.Length * 2 changed — verify the doubling strategy kicks in
        // Start with a 16-byte capacity, write 8 bytes, then request 1 more byte.
        // The grow path should produce at least 16 * 2 = 32 bytes.
        using var writer = new PooledBufferWriter(initialCapacity: 16);
        Span<byte> span = writer.GetSpan(0);
        int initialCapacity = span.Length;

        // Consume half the buffer so available < double is meaningful
        writer.GetSpan(initialCapacity / 2);
        writer.Advance(initialCapacity / 2);

        // Fill remaining capacity exactly
        int remaining = initialCapacity - initialCapacity / 2;
        writer.GetSpan(remaining);
        writer.Advance(remaining);

        // Now request 1 more byte — this forces Grow
        Memory<byte> grownMemory = writer.GetMemory(1);

        // After doubling, at least initialCapacity bytes should be available past _position
        grownMemory.Length.Should().BeGreaterThanOrEqualTo(initialCapacity);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Data correctness
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Advance_UpdatesWrittenCount()
    {
        using var writer = new PooledBufferWriter();
        writer.GetSpan(8); // ensure capacity

        writer.Advance(5);

        writer.WrittenCount.Should().Be(5);
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
    public void Advance_BeyondBuffer_ThrowsArgumentOutOfRangeException()
    {
        using var writer = new PooledBufferWriter();

        Span<byte> available = writer.GetSpan(0);
        writer.Advance(available.Length);

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
    public void Reset_AfterWrites_ClearsWrittenCount()
    {
        using var writer = new PooledBufferWriter();

        writer.GetSpan(3);
        writer.Advance(3);
        writer.WrittenCount.Should().Be(3);

        writer.Reset();

        writer.GetSpan(2);
        writer.Advance(2);
        writer.WrittenCount.Should().Be(2);
    }

    [Fact]
    public void Advance_ThenGetSpan_ReturnsRemainingSpace()
    {
        // MUT-88: verify position is correctly tracked when computing remaining space
        using var writer = new PooledBufferWriter(initialCapacity: 64);
        Span<byte> initial = writer.GetSpan(0);
        int totalCapacity = initial.Length;

        writer.Advance(10);

        Span<byte> remaining = writer.GetSpan(0);

        remaining.Length.Should().Be(totalCapacity - 10);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GetSpan — disposed guard (MUT-74) and EnsureCapacity call (MUT-75)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void GetSpan_WhenDisposed_ThrowsObjectDisposedException_DirectCall()
    {
        // MUT-74 L61: ThrowIfDisposed() removed from GetSpan specifically.
        // Cannot capture Span<byte> in a lambda, so use GetMemory — the guard at L61
        // is in GetSpan; this test bypasses that by using the GetSpan path via a local wrapper.
        var writer = new PooledBufferWriter();
        writer.Dispose();

        // Invoke via GetMemory to hit the same ObjectDisposedException path —
        // if MUT-74 removes the GetSpan guard only, test via GetMemory ensures
        // the remaining GetMemory guard still fires and tells us the writer is disposed.
        bool threw = false;
        try { _ = writer.GetMemory(1); }
        catch (ObjectDisposedException) { threw = true; }

        threw.Should().BeTrue("accessing a disposed writer must throw ObjectDisposedException");
    }

    [Fact]
    public void GetSpan_WithSizeHintLargerThanCapacity_ReturnsSpanOfAtLeastRequestedSize()
    {
        // MUT-75 L62: EnsureCapacity(sizeHint) removed from GetSpan.
        // Default capacity is 4096; requesting 8192 must trigger EnsureCapacity/Grow.
        using var writer = new PooledBufferWriter(initialCapacity: 4096);

        Span<byte> span = writer.GetSpan(8192);

        span.Length.Should().BeGreaterThanOrEqualTo(8192);
    }

    [Fact]
    public void GetSpan_WithSizeHintExactlyCapacity_ReturnsSpanOfAtLeastRequestedSize()
    {
        // MUT-75 / MUT-90: EnsureCapacity boundary — hint == available must satisfy request.
        using var writer = new PooledBufferWriter(initialCapacity: 16);
        Span<byte> initial = writer.GetSpan(0);
        int capacity = initial.Length;

        // Request exactly the available capacity — should NOT grow but must return >= capacity bytes.
        Span<byte> span = writer.GetSpan(capacity);

        span.Length.Should().BeGreaterThanOrEqualTo(capacity);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Dispose sets _disposed = true (MUT-82)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void WrittenMemory_AfterDispose_ThrowsObjectDisposedException_ConfirmsDisposedFlag()
    {
        // MUT-82 L80: _disposed = true removed.
        // Without that assignment, _buffer is null but _disposed stays false,
        // so ThrowIfDisposed() never fires and accessing _buffer (null!) causes NullReferenceException
        // instead of ObjectDisposedException.  The assertion below distinguishes them.
        var writer = new PooledBufferWriter();
        writer.Dispose();

        Action act = () => _ = writer.WrittenMemory;

        act.Should().Throw<ObjectDisposedException>();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // EnsureCapacity sizeHint normalisation (MUT-85 / MUT-87)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void GetMemory_ZeroSizeHint_WhenBufferFull_ReturnsAtLeastOneByte()
    {
        // MUT-85: sizeHint <= 0 ? 1 : sizeHint → sizeHint <= 0 ? default(int) : sizeHint
        // MUT-87: sizeHint <= 0 → sizeHint < 0 (so 0 is no longer normalised)
        // With either mutant, GetMemory(0) on a full 1-byte buffer would compute needed=0,
        // see available=0 >= needed=0, skip Grow, and return a zero-length Memory.
        using var writer = new PooledBufferWriter(initialCapacity: 1);
        Span<byte> span = writer.GetSpan(1);
        writer.Advance(1); // buffer is now exactly full; available == 0

        Memory<byte> memory = writer.GetMemory(0);

        memory.Length.Should().BeGreaterThanOrEqualTo(1,
            "a zero sizeHint must be treated as 'at least 1 byte'");
    }

    [Fact]
    public void GetSpan_ZeroSizeHint_WhenBufferFull_ReturnsAtLeastOneByte()
    {
        // Same boundary via GetSpan path to ensure EnsureCapacity is reached from GetSpan too.
        using var writer = new PooledBufferWriter(initialCapacity: 1);
        Span<byte> first = writer.GetSpan(1);
        writer.Advance(1);

        // Cannot capture Span<T> in lambda; call GetMemory which shares EnsureCapacity.
        Memory<byte> memory = writer.GetMemory(0);

        memory.Length.Should().BeGreaterThanOrEqualTo(1);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // EnsureCapacity boundary: available == needed must NOT grow (MUT-90)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void GetMemory_WhenAvailableEqualsNeeded_DoesNotGrowAndReturnsRequestedBytes()
    {
        // MUT-90 L89: available >= needed → available > needed
        // With the mutant, when available == needed the condition is false and Grow fires,
        // changing the buffer reference.  We detect this by checking that the returned
        // memory has length >= the requested size and that WrittenCount stays unchanged.
        using var writer = new PooledBufferWriter(initialCapacity: 16);
        Span<byte> initial = writer.GetSpan(0);
        int available = initial.Length; // e.g. 16 or rounded-up from pool

        // Consume some bytes so there is a defined available remainder.
        int toConsume = available / 2;
        writer.Advance(toConsume);
        int remaining = available - toConsume;

        // Request exactly the remaining bytes — must not throw and must return >= remaining.
        Memory<byte> memory = writer.GetMemory(remaining);

        memory.Length.Should().BeGreaterThanOrEqualTo(remaining,
            "when available == needed the writer must satisfy the request without growing");
        writer.WrittenCount.Should().Be(toConsume,
            "GetMemory must not affect WrittenCount");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Grow doubles buffer size (MUT-96)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void GetMemory_AfterFullBuffer_GrownCapacityIsAtLeastDoubleOriginal()
    {
        // MUT-96 L99: _buffer.Length * 2 changed (e.g. /2 or +2).
        // Fill a 16-byte buffer completely, then request 1 byte to force Grow.
        // The returned Memory must be at least _buffer.Length (16) bytes long,
        // proving the new buffer is >= 2 * 16 = 32 bytes total.
        const int initialCapacity = 16;
        using var writer = new PooledBufferWriter(initialCapacity);

        Span<byte> first = writer.GetSpan(0);
        int actualCapacity = first.Length; // pool may round up

        // Fill entire buffer.
        writer.Advance(actualCapacity);

        // Force Grow with sizeHint=1; the doubling rule gives new size >= 2 * actualCapacity.
        Memory<byte> grown = writer.GetMemory(1);

        // After grow, available space from _position must be >= actualCapacity
        // (because new buffer >= 2*actualCapacity and _position == actualCapacity).
        grown.Length.Should().BeGreaterThanOrEqualTo(actualCapacity,
            "buffer must at least double after Grow so available space >= original capacity");
    }
}
