using System.Threading.Channels;
using AwesomeAssertions;
using BareWire.Abstractions;

namespace BareWire.UnitTests.Abstractions;

public sealed class PublishFlowControlOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var options = new PublishFlowControlOptions();

        options.MaxPendingPublishes.Should().Be(10_000);
        options.MaxPendingBytes.Should().Be(268_435_456L);
        options.FullMode.Should().Be(BoundedChannelFullMode.Wait);
    }
}
