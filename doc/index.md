# BareWire Documentation

Welcome to the BareWire documentation. BareWire is a high-performance async messaging library for .NET 10 / C# 14 — an alternative to MassTransit built around raw-first serialization, zero-copy pipelines, manual topology, and deterministic memory usage.

## Table of Contents

1. [Getting Started](getting-started.md) — installation, first publisher and consumer
2. [Configuration](configuration.md) — bus setup, RabbitMQ transport, DI registration
3. [Publishing and Consuming](publishing-and-consuming.md) — publish/subscribe, request-response, raw messages
4. [Topology](topology.md) — exchanges, queues, bindings, routing keys
5. [Flow Control and Backpressure](flow-control.md) — credit-based flow control, publish-side backpressure
6. [Retry and Dead Letter Queues](retry-and-dlq.md) — retry policies, DLX routing, DLQ consumers
7. [Saga State Machines](saga.md) — state machines, compensable activities, scheduled timeouts
8. [Transactional Outbox](outbox.md) — exactly-once delivery, inbox deduplication
9. [Observability](observability.md) — OpenTelemetry, metrics, health checks
10. [Advanced Patterns](advanced-patterns.md) — partitioning, multi-consumer endpoints, raw interop
11. [Aspire Integration](aspire-integration.md) — orchestrating BareWire apps with .NET Aspire

## Allocation Characteristics

- **PublishRaw** — constant **136 B** regardless of payload size (100 B → 10 KB). Pre-serialized `ReadOnlyMemory<byte>` passes through without copying.
- **PublishTyped** — **~544 B fixed overhead + serialized payload size**. The serialization boundary copy (`.ToArray()`) is architecturally required — `OutboundMessage` must outlive the pooled writer scope.
- **Serialization (raw)** — constant **448 B** regardless of payload size. `PooledBufferWriter` rents from `ArrayPool<byte>.Shared` (ADR-003 zero-copy pipeline).

Full benchmark report: [benchmark-report.md](benchmark-report.md)

## Samples

All documentation references working code from the `samples/` directory. You can run all samples simultaneously using the Aspire AppHost:

```bash
dotnet run --project samples/BareWire.Samples.AppHost/
```

| Sample | Description |
|---|---|
| `BasicPublishConsume` | Publish/subscribe with PostgreSQL persistence |
| `RequestResponse` | Synchronous request-response with validation |
| `RawMessageInterop` | Interop with legacy systems via raw JSON |
| `RabbitMQ` | Full-featured example with SAGA, outbox, observability |
| `BackpressureDemo` | Consume-side and publish-side flow control |
| `RetryAndDlq` | Retry policies and dead letter queue handling |
| `SagaOrderFlow` | Complex order lifecycle with compensable activities |
| `TransactionalOutbox` | Exactly-once delivery via transactional outbox |
| `ObservabilityShowcase` | Distributed tracing with OpenTelemetry |
| `MultiConsumerPartitioning` | Per-correlation ordering with partitioned consumers |
