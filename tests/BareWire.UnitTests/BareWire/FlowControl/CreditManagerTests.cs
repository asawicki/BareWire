using AwesomeAssertions;
using BareWire.Abstractions;
using BareWire.FlowControl;

namespace BareWire.UnitTests.Core.FlowControl;

public sealed class CreditManagerTests
{
    private static FlowControlOptions DefaultOptions(int maxMessages = 10, long maxBytes = 1_000) =>
        new() { MaxInFlightMessages = maxMessages, MaxInFlightBytes = maxBytes };

    [Fact]
    public void TryGrantCredits_WhenEmpty_ReturnsRequested()
    {
        var manager = new CreditManager(DefaultOptions(maxMessages: 10));

        int granted = manager.TryGrantCredits(5);

        granted.Should().Be(5);
    }

    [Fact]
    public void TryGrantCredits_WhenAtLimit_ReturnsZero()
    {
        var manager = new CreditManager(DefaultOptions(maxMessages: 5));
        manager.TrackInflight(5, 0);

        int granted = manager.TryGrantCredits(1);

        granted.Should().Be(0);
    }

    [Fact]
    public void TryGrantCredits_WhenPartiallyFull_ReturnsAvailable()
    {
        var manager = new CreditManager(DefaultOptions(maxMessages: 10));
        manager.TrackInflight(8, 0);

        // Only 2 slots remain; requesting 5 should yield 2.
        int granted = manager.TryGrantCredits(5);

        granted.Should().Be(2);
    }

    [Fact]
    public void TrackInflight_IncrementsCounters()
    {
        var manager = new CreditManager(DefaultOptions());

        manager.TrackInflight(3, 500);

        manager.InflightCount.Should().Be(3);
        manager.InflightBytes.Should().Be(500);
    }

    [Fact]
    public void ReleaseInflight_DecrementsCounters()
    {
        var manager = new CreditManager(DefaultOptions());
        manager.TrackInflight(4, 800);

        manager.ReleaseInflight(4, 800);

        manager.InflightCount.Should().Be(0);
        manager.InflightBytes.Should().Be(0);
    }

    [Fact]
    public void UtilizationPercent_CalculatesCorrectly()
    {
        var manager = new CreditManager(DefaultOptions(maxMessages: 100));
        manager.TrackInflight(75, 0);

        manager.UtilizationPercent.Should().Be(75.0);
    }

    [Fact]
    public void IsAtCapacity_WhenBytesExceeded_ReturnsTrue()
    {
        var manager = new CreditManager(DefaultOptions(maxMessages: 100, maxBytes: 1_000));

        // Exceed byte limit without hitting message count limit.
        manager.TrackInflight(1, 1_001);

        manager.IsAtCapacity.Should().BeTrue();
    }

    [Fact]
    public void TryGrantCredits_ZeroRequested_ReturnsZero()
    {
        var manager = new CreditManager(DefaultOptions(maxMessages: 10));

        int granted = manager.TryGrantCredits(0);

        granted.Should().Be(0);
    }

    [Fact]
    public void ReleaseInflight_MoreThanTracked_ClampsToZero()
    {
        var manager = new CreditManager(DefaultOptions(maxMessages: 10, maxBytes: 1_000));
        manager.TrackInflight(2, 100);

        // Release more than was tracked — counts must not go below zero.
        manager.ReleaseInflight(5, 500);

        manager.InflightCount.Should().Be(0);
        manager.InflightBytes.Should().Be(0);
    }
}
