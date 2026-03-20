using System.Diagnostics.Metrics;
using AwesomeAssertions;
using BareWire.Abstractions.Observability;
using BareWire.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;

namespace BareWire.UnitTests.Observability;

public sealed class ObservabilityServiceCollectionExtensionsTests
{
    [Fact]
    public void AddBareWireObservability_CalledTwice_RegistersDuplicateHealthCheck()
    {
        // This test documents the bug: calling AddBareWireObservability twice
        // registers the "barewire" health check twice, which throws at runtime.
        // Callers must avoid calling it twice (e.g. via AddServiceDefaults + direct call).

        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddBareWireObservability(cfg => { cfg.EnableOpenTelemetry = false; });
        services.AddBareWireObservability(cfg => { cfg.EnableOpenTelemetry = false; });

        // Act — building the provider succeeds but resolving HealthCheckService fails
        using var provider = services.BuildServiceProvider();
        Action act = () => provider.GetRequiredService<HealthCheckService>();

        // Assert — duplicate "barewire" name causes ArgumentException
        act.Should().Throw<ArgumentException>()
            .Which.Message.Should().Contain("barewire");
    }

    [Fact]
    public void AddBareWireObservability_CalledOnce_HealthCheckResolvesSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddBareWireObservability(cfg => { cfg.EnableOpenTelemetry = false; });

        // Act
        using var provider = services.BuildServiceProvider();
        var healthCheckService = provider.GetService<HealthCheckService>();

        // Assert
        healthCheckService.Should().NotBeNull();
    }

    [Fact]
    public void AddBareWireObservability_WithMetrics_BareWireMetricsResolvesSuccessfully()
    {
        // Regression test: BareWireMetrics has an internal constructor, so TryAddSingleton<BareWireMetrics>()
        // (auto-resolution) would throw at resolve time because DI cannot find the constructor.
        // The fix registers a factory lambda: sp => new BareWireMetrics(sp.GetRequiredService<IMeterFactory>()).

        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Register a fake IMeterFactory (same pattern used in BareWireMetricsTests) so that
        // the factory lambda registered by AddBareWireObservability can resolve IMeterFactory
        // without requiring the full Microsoft.Extensions.Hosting stack.
        var meterFactory = Substitute.For<IMeterFactory>();
        meterFactory.Create(Arg.Any<MeterOptions>()).Returns(new Meter("BareWire.Test"));
        services.AddSingleton(meterFactory);

        services.AddBareWireObservability(cfg => { cfg.EnableOpenTelemetry = false; });

        // Act
        using var provider = services.BuildServiceProvider();
        Action act = () => provider.GetService(typeof(BareWireMetrics));

        // Assert — must not throw (would have thrown before the fix)
        act.Should().NotThrow();
    }

    [Fact]
    public void AddBareWireObservability_WithMetrics_BareWireInstrumentationResolvesSuccessfully()
    {
        // Regression test: IBareWireInstrumentation (BareWireInstrumentation) depends on BareWireMetrics.
        // If BareWireMetrics failed to resolve (internal ctor, no factory), the full instrumentation
        // chain would also fail. Verifies end-to-end DI wiring after the factory-lambda fix.

        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Register a fake IMeterFactory so the factory lambda in AddBareWireObservability can
        // construct BareWireMetrics, which BareWireInstrumentation depends on.
        var meterFactory = Substitute.For<IMeterFactory>();
        meterFactory.Create(Arg.Any<MeterOptions>()).Returns(new Meter("BareWire.Test"));
        services.AddSingleton(meterFactory);

        services.AddBareWireObservability(cfg => { cfg.EnableOpenTelemetry = false; });

        // Act
        using var provider = services.BuildServiceProvider();
        var instrumentation = provider.GetService<IBareWireInstrumentation>();

        // Assert — the full instrumentation chain resolves successfully
        instrumentation.Should().NotBeNull();
    }
}
