using System.Buffers;
using System.Text.Json;

using BareWire.Abstractions.Serialization;
using BareWire.Core.Buffers;
using BareWire.Serialization.Json;

using BenchmarkDotNet.Attributes;

namespace BareWire.Benchmarks;

internal sealed record BenchmarkOrder(
    string OrderId,
    decimal Amount,
    string Currency,
    List<BenchmarkOrderItem>? Items);

internal sealed record BenchmarkOrderItem(
    string ProductId,
    string Name,
    int Quantity,
    decimal Price);

[MemoryDiagnoser(displayGenColumns: true)]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "BenchmarkDotNet manages object lifetime via [GlobalCleanup].")]
public class SerializationBenchmarks
{
    [Params(100, 1_000, 10_000, 100_000)]
    public int PayloadSizeBytes { get; set; }

    private SystemTextJsonSerializer _serializer = null!;
    private SystemTextJsonRawDeserializer _deserializer = null!;
    private BareWireEnvelopeSerializer _envelopeSerializer = null!;
    private PooledBufferWriter _writer = null!;
    private BenchmarkOrder _payload = null!;
    private ReadOnlySequence<byte> _serializedRaw;
    private ReadOnlySequence<byte> _serializedEnvelope;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _serializer = new SystemTextJsonSerializer();
        _deserializer = new SystemTextJsonRawDeserializer();
        _envelopeSerializer = new BareWireEnvelopeSerializer();
        _writer = new PooledBufferWriter(initialCapacity: PayloadSizeBytes * 4);
        _payload = GeneratePayload(PayloadSizeBytes);

        _serializedRaw = PreSerialize(_serializer, _payload);
        _serializedEnvelope = PreSerialize(_envelopeSerializer, _payload);
    }

    [Benchmark]
    public int Serialize_Raw()
    {
        _writer.Reset();
        _serializer.Serialize(_payload, _writer);
        return _writer.WrittenCount;
    }

    [Benchmark]
    public int Serialize_Envelope()
    {
        _writer.Reset();
        _envelopeSerializer.Serialize(_payload, _writer);
        return _writer.WrittenCount;
    }

    [Benchmark]
    public object? Deserialize_Raw()
    {
        return _deserializer.Deserialize<BenchmarkOrder>(_serializedRaw);
    }

    [Benchmark]
    public object? Deserialize_Envelope()
    {
        return _envelopeSerializer.Deserialize<BenchmarkOrder>(_serializedEnvelope);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _writer.Dispose();
    }

    private static BenchmarkOrder GeneratePayload(int targetSize)
    {
        // Start with an estimate: each item serializes to roughly 80 bytes.
        const int bytesPerItem = 80;
        int estimatedItems = Math.Max(1, (targetSize - 60) / bytesPerItem);

        BenchmarkOrder order = BuildOrder(estimatedItems);

        // Iteratively adjust item count until serialized size is within ±10% of target.
        for (int attempt = 0; attempt < 20; attempt++)
        {
            int actualSize = MeasureSerializedSize(order);
            double ratio = (double)actualSize / targetSize;

            if (ratio >= 0.9 && ratio <= 1.1)
                break;

            // Scale item count proportionally to the size difference.
            estimatedItems = Math.Max(1, (int)(estimatedItems * ((double)targetSize / actualSize)));
            order = BuildOrder(estimatedItems);
        }

        return order;
    }

    private static BenchmarkOrder BuildOrder(int itemCount)
    {
        var items = new List<BenchmarkOrderItem>(itemCount);

        for (int i = 0; i < itemCount; i++)
        {
            items.Add(new BenchmarkOrderItem(
                ProductId: $"PROD-{i:D5}",
                Name: $"Product Name {i}",
                Quantity: (i % 10) + 1,
                Price: 9.99m + i));
        }

        return new BenchmarkOrder(
            OrderId: "ORD-20260318-001",
            Amount: items.Sum(x => x.Price * x.Quantity),
            Currency: "PLN",
            Items: items);
    }

    private static int MeasureSerializedSize(BenchmarkOrder order)
    {
        using var tempWriter = new PooledBufferWriter(initialCapacity: 4096);
        using var jsonWriter = new System.Text.Json.Utf8JsonWriter(tempWriter);
        System.Text.Json.JsonSerializer.Serialize(jsonWriter, order);
        jsonWriter.Flush();
        return tempWriter.WrittenCount;
    }

    private ReadOnlySequence<byte> PreSerialize(IMessageSerializer serializer, BenchmarkOrder message)
    {
        using var tempWriter = new PooledBufferWriter(initialCapacity: PayloadSizeBytes * 4);
        serializer.Serialize(message, tempWriter);
        byte[] copy = tempWriter.WrittenSpan.ToArray();
        return new ReadOnlySequence<byte>(copy);
    }
}
