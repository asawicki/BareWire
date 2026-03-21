using BenchmarkDotNet.Attributes;
using BareWire.Testing;

namespace BareWire.Benchmarks;

/// <summary>
/// Benchmarks for publish-side throughput through the in-memory transport.
/// Measures outbound pipeline performance (channel enqueue + transport dispatch),
/// not serialization (the test harness uses a no-op serializer).
/// </summary>
/// <remarks>
/// Performance targets:
/// <list type="bullet">
/// <item><description>PublishTyped: &gt; 500K msgs/s, &lt; 768 B/msg</description></item>
/// <item><description>PublishRaw: &gt; 1M msgs/s, &lt; 512 B/msg</description></item>
/// </list>
/// NOTE: [EventPipeProfiler] is intentionally omitted — BenchmarkDotNet has a known bug with
/// .NET 10 where runtime detection treats it as v1 (https://github.com/dotnet/BenchmarkDotNet/issues/2699).
/// Add [EventPipeProfiler] after BenchmarkDotNet ships a fix.
/// </remarks>
[MemoryDiagnoser(displayGenColumns: true)]
public class PublishBenchmarks
{
    private BareWireTestHarness _harness = null!;
    private ReadOnlyMemory<byte> _rawPayload;

    private static readonly BenchmarkMessage _typedMessage = new(
        Id: "order-bench-001",
        Amount: 99.99m,
        Currency: "USD");

    [GlobalSetup]
    public async Task SetupAsync()
    {
        _harness = await BareWireTestHarness.CreateAsync();

        // Pre-create representative JSON payload (~100 B) to avoid allocation in the hot path.
        // Matches the shape of BenchmarkMessage so raw benchmarks measure the same data volume.
        byte[] payload = System.Text.Encoding.UTF8.GetBytes(
            """{"Id":"order-bench-001","Amount":99.99,"Currency":"USD"}""");
        _rawPayload = new ReadOnlyMemory<byte>(payload);
    }

    [GlobalCleanup]
    public async Task CleanupAsync()
        => await _harness.DisposeAsync();

    /// <summary>
    /// Publishes a typed <see cref="BenchmarkMessage"/> through the full outbound pipeline.
    /// The in-memory transport accepts the message synchronously after channel enqueue.
    /// Target: &gt; 500K msgs/s, &lt; 768 B/msg.
    /// </summary>
    [Benchmark]
    public Task PublishTyped()
        => _harness.Bus.PublishAsync(_typedMessage);

    /// <summary>
    /// Publishes a pre-serialized raw payload, bypassing typed serialization entirely.
    /// Measures pure channel + transport overhead with zero per-call allocations.
    /// Target: &gt; 1M msgs/s, &lt; 512 B/msg.
    /// </summary>
    [Benchmark]
    public Task PublishRaw()
        => _harness.Bus.PublishRawAsync(_rawPayload, contentType: "application/json");
}

/// <summary>
/// Benchmarks for publish-side allocation scaling with payload size.
/// Shows that per-message overhead is ~544 B fixed + payload size (due to serialization
/// boundary copy in <c>MessagePipeline.ProcessOutboundAsync</c> — see ADR-003 / C-1).
/// </summary>
[MemoryDiagnoser(displayGenColumns: true)]
public class PublishPayloadScalingBenchmarks
{
    [Params(100, 1_000, 10_000)]
    public int PayloadSizeBytes { get; set; }

    private BareWireTestHarness _harness = null!;
    private ReadOnlyMemory<byte> _rawPayload;

    [GlobalSetup]
    public async Task SetupAsync()
    {
        _harness = await BareWireTestHarness.CreateAsync();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        // Build a JSON payload of approximately the target size.
        // Uses a padding field to reach the desired byte count.
        string padding = new('x', Math.Max(0, PayloadSizeBytes - 50));
        byte[] payload = System.Text.Encoding.UTF8.GetBytes(
            $$"""{"Id":"bench","Amount":1.00,"Currency":"USD","Pad":"{{padding}}"}""");
        _rawPayload = new ReadOnlyMemory<byte>(payload);
    }

    [GlobalCleanup]
    public async Task CleanupAsync()
        => await _harness.DisposeAsync();

    /// <summary>
    /// Publishes a raw payload of varying size to show allocation scaling.
    /// Fixed overhead (~544 B) + payload size.
    /// </summary>
    [Benchmark]
    public Task PublishRaw_Scaled()
        => _harness.Bus.PublishRawAsync(_rawPayload, contentType: "application/json");
}

/// <summary>
/// A representative message type for publish benchmarks.
/// Approximately 100 B when serialized to JSON.
/// </summary>
public sealed record BenchmarkMessage(string Id, decimal Amount, string Currency);
