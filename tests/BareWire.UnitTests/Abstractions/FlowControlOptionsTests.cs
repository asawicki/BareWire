using System.Threading.Channels;
using AwesomeAssertions;
using BareWire.Abstractions;

namespace BareWire.UnitTests.Abstractions;

public sealed class FlowControlOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var options = new FlowControlOptions();

        options.MaxInFlightMessages.Should().Be(100);
        options.MaxInFlightBytes.Should().Be(67_108_864L);
        options.InternalQueueCapacity.Should().Be(1_000);
        options.FullMode.Should().Be(BoundedChannelFullMode.Wait);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var options = new FlowControlOptions
        {
            MaxInFlightMessages = 50,
            MaxInFlightBytes = 1_000_000L,
            InternalQueueCapacity = 500,
            FullMode = BoundedChannelFullMode.DropOldest,
        };

        options.MaxInFlightMessages.Should().Be(50);
        options.MaxInFlightBytes.Should().Be(1_000_000L);
        options.InternalQueueCapacity.Should().Be(500);
        options.FullMode.Should().Be(BoundedChannelFullMode.DropOldest);
    }
}
