using System.Buffers;

namespace BareWire.Core.Buffers;

// IBufferWriter<byte> backed by ArrayPool<byte>.Shared.
// All buffers are rented from the pool — never new byte[]. ADR-003 compliant.
// Not thread-safe; intended for single-threaded serialization use within a pipeline stage.
internal sealed class PooledBufferWriter : IBufferWriter<byte>, IDisposable
{
    private byte[] _buffer;
    private int _position;
    private bool _disposed;

    internal PooledBufferWriter(int initialCapacity = 4096)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(initialCapacity, 1);
        _buffer = ArrayPool<byte>.Shared.Rent(initialCapacity);
    }

    internal int WrittenCount => _position;

    internal ReadOnlyMemory<byte> WrittenMemory
    {
        get
        {
            ThrowIfDisposed();
            return _buffer.AsMemory(0, _position);
        }
    }

    internal ReadOnlySpan<byte> WrittenSpan
    {
        get
        {
            ThrowIfDisposed();
            return _buffer.AsSpan(0, _position);
        }
    }

    public void Advance(int count)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        int available = _buffer.Length - _position;
        if (count > available)
            throw new ArgumentOutOfRangeException(nameof(count), "Cannot advance past end of buffer.");

        _position += count;
    }

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        ThrowIfDisposed();
        EnsureCapacity(sizeHint);
        return _buffer.AsMemory(_position);
    }

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        ThrowIfDisposed();
        EnsureCapacity(sizeHint);
        return _buffer.AsSpan(_position);
    }

    // Reset the write position without returning the buffer to the pool.
    // Allows reuse of the writer (and its rented buffer) across multiple serializations.
    internal void Reset()
    {
        ThrowIfDisposed();
        _position = 0;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = null!;
    }

    private void EnsureCapacity(int sizeHint)
    {
        int needed = sizeHint <= 0 ? 1 : sizeHint;
        int available = _buffer.Length - _position;

        if (available >= needed)
            return;

        Grow(needed);
    }

    // Rents a new buffer (at least 2x or position+sizeHint), copies existing data, returns old buffer to pool.
    // Never allocates with new byte[]. ADR-003 compliant.
    private void Grow(int sizeHint)
    {
        int minimumSize = Math.Max(_buffer.Length * 2, _position + sizeHint);
        byte[] newBuffer = ArrayPool<byte>.Shared.Rent(minimumSize);

        Buffer.BlockCopy(_buffer, 0, newBuffer, 0, _position);

        ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = newBuffer;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
