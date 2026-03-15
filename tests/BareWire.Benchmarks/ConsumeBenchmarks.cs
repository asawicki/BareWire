using BenchmarkDotNet.Attributes;

namespace BareWire.Benchmarks;

// ConsumeBenchmarks require consume loop + consumer dispatch (not yet implemented).
// Implementation deferred until consume pipeline is available (Phase 2+).
[MemoryDiagnoser(displayGenColumns: true)]
public sealed class ConsumeBenchmarks
{
}
