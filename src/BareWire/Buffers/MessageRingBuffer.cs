using System.Threading;

namespace BareWire.Buffers;

// SPSC (single-producer, single-consumer) lock-free ring buffer.
// Head is the write index (producer-owned), tail is the read index (consumer-owned).
// Volatile.Read/Write provide the necessary memory ordering for SPSC correctness.
// Capacity is always rounded up to the next power of two so that masking replaces modulo.
internal sealed class MessageRingBuffer<T>
{
    private readonly T[] _buffer;
    private readonly int _mask;
    private int _head; // write position, owned by producer
    private int _tail; // read position, owned by consumer

    internal MessageRingBuffer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);

        capacity = NextPowerOfTwo(capacity);
        _buffer = new T[capacity];
        _mask = capacity - 1;
    }

    internal int Capacity => _buffer.Length;

    internal int Count
    {
        get
        {
            int head = Volatile.Read(ref _head);
            int tail = Volatile.Read(ref _tail);
            return head - tail;
        }
    }

    internal bool IsEmpty => Count == 0;

    internal bool IsFull => Count >= _buffer.Length;

    // Returns false if the buffer is full. Zero-alloc — no heap activity in hot path.
    internal bool TryWrite(T item)
    {
        int head = Volatile.Read(ref _head);
        int tail = Volatile.Read(ref _tail);

        if (head - tail >= _buffer.Length)
            return false;

        _buffer[head & _mask] = item;

        // Store-release: make the write visible to the consumer before advancing head.
        Volatile.Write(ref _head, head + 1);
        return true;
    }

    // Returns false if the buffer is empty. Zero-alloc — no heap activity in hot path.
    internal bool TryRead(out T item)
    {
        int tail = Volatile.Read(ref _tail);
        int head = Volatile.Read(ref _head);

        if (tail == head)
        {
            item = default!;
            return false;
        }

        item = _buffer[tail & _mask];

        // Clear the slot to release any GC references held by the slot.
        _buffer[tail & _mask] = default!;

        // Store-release: make the read visible to the producer before advancing tail.
        Volatile.Write(ref _tail, tail + 1);
        return true;
    }

    private static int NextPowerOfTwo(int value)
    {
        if (value <= 1)
            return 1;

        // Bit-twiddling to round up to the next power of two.
        value--;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return value + 1;
    }
}
