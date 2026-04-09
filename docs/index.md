# BareWire

<div class="hero">
  <div class="row align-items-center">
    <div class="col-12 col-lg-7">
      <h1 class="display-2 fw-bold">BareWire</h1>
      <p class="lead">
        A high-performance async messaging library for <strong>.NET 10 / C# 14</strong>.
        Raw-first. Zero-copy. Deterministic. A MassTransit alternative that gets out of your way.
      </p>
      <p>
        <a class="btn btn-primary btn-lg me-2" href="articles/getting-started.md">Get Started</a>
        <a class="btn btn-outline-primary btn-lg" href="api/index.md">API Reference</a>
      </p>
    </div>
    <div class="col-12 col-lg-5 d-none d-lg-block text-center">
      <img src="images/logo.png" alt="BareWire logo" class="hero-logo" />
    </div>
  </div>
</div>

## Why BareWire

<div class="row feature-row">
  <div class="col-12 col-md-4">
    <div class="feature-card">
      <h3>Raw-first</h3>
      <p>Default serializer produces raw JSON — no envelope, no wrapping. The envelope is opt-in, not the other way around. Your wire format is your wire format.</p>
    </div>
  </div>
  <div class="col-12 col-md-4">
    <div class="feature-card">
      <h3>Zero-copy pipeline</h3>
      <p><code>IBufferWriter&lt;byte&gt;</code> and <code>ReadOnlySequence&lt;byte&gt;</code> with <code>ArrayPool</code> throughout. No <code>new byte[]</code> in the hot path. Deterministic memory usage under load.</p>
    </div>
  </div>
  <div class="col-12 col-md-4">
    <div class="feature-card">
      <h3>Manual topology</h3>
      <p>No auto-topology magic. You declare exchanges, queues, and bindings — or turn on opt-in auto-configuration. Predictable deployments, no surprises in production.</p>
    </div>
  </div>
</div>

## Performance targets

<div class="row stats-row">
  <div class="col-6 col-md-3">
    <div class="stat">
      <strong class="display-5">&lt; 768 B</strong>
      <span>per message publish alloc</span>
    </div>
  </div>
  <div class="col-6 col-md-3">
    <div class="stat">
      <strong class="display-5">&lt; 512 B</strong>
      <span>per op consume alloc</span>
    </div>
  </div>
  <div class="col-6 col-md-3">
    <div class="stat">
      <strong class="display-5">&gt; 500K</strong>
      <span>msgs/s publish</span>
    </div>
  </div>
  <div class="col-6 col-md-3">
    <div class="stat">
      <strong class="display-5">&gt; 300K</strong>
      <span>msgs/s consume</span>
    </div>
  </div>
</div>

## Core concepts

- **[Getting Started](articles/getting-started.md)** — install the package and publish your first message
- **[Publishing and Consuming](articles/publishing-and-consuming.md)** — producers, consumers, request/response
- **[Configuration](articles/configuration.md)** — fluent API, options, dependency injection
- **[Topology](articles/topology.md)** — exchanges, queues, bindings, manual vs auto
- **[Flow Control](articles/flow-control.md)** — credit-based backpressure, bounded channels
- **[Saga](articles/saga.md)** and **[Outbox](articles/outbox.md)** — stateful workflows and transactional delivery
- **[Observability](articles/observability.md)** — OpenTelemetry, metrics, tracing
- **[MassTransit Interop](articles/masstransit-interop.md)** — bridge to existing MassTransit services

## About

BareWire is developed by [Wizard-Software](https://github.com/Wizard-Software) and hosted on GitHub at [Wizard-Software/BareWire](https://github.com/Wizard-Software/BareWire). MIT licensed.
