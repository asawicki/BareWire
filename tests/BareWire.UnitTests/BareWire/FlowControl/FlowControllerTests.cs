using AwesomeAssertions;
using BareWire.Abstractions;
using BareWire.FlowControl;
using Microsoft.Extensions.Logging.Abstractions;

namespace BareWire.UnitTests.Core.FlowControl;

public sealed class FlowControllerTests
{
    private static FlowController CreateController() =>
        new(NullLogger<FlowController>.Instance);

    private static FlowControlOptions OptionsWithLimit(int maxMessages = 100) =>
        new() { MaxInFlightMessages = maxMessages };

    [Fact]
    public void GetOrCreateManager_ReturnsSameInstanceForSameEndpoint()
    {
        var controller = CreateController();
        var options = OptionsWithLimit();

        CreditManager first = controller.GetOrCreateManager("my-queue", options);
        CreditManager second = controller.GetOrCreateManager("my-queue", options);

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void GetOrCreateManager_ReturnsDifferentForDifferentEndpoints()
    {
        var controller = CreateController();
        var options = OptionsWithLimit();

        CreditManager alpha = controller.GetOrCreateManager("queue-alpha", options);
        CreditManager beta = controller.GetOrCreateManager("queue-beta", options);

        alpha.Should().NotBeSameAs(beta);
    }

    [Fact]
    public void CheckHealth_WhenLowUtilization_ReturnsHealthy()
    {
        var controller = CreateController();
        var options = OptionsWithLimit(maxMessages: 100);
        CreditManager manager = controller.GetOrCreateManager("healthy-queue", options);

        // 50% utilization — well below the 90% alert threshold.
        manager.TrackInflight(50, 0);

        BusStatus status = controller.CheckHealth("healthy-queue");

        status.Should().Be(BusStatus.Healthy);
    }

    [Fact]
    public void CheckHealth_WhenAbove90Percent_ReturnsDegraded()
    {
        var controller = CreateController();
        var options = OptionsWithLimit(maxMessages: 100);
        CreditManager manager = controller.GetOrCreateManager("busy-queue", options);

        // 95% utilization — above the 90% health alert threshold (ADR-004).
        manager.TrackInflight(95, 0);

        BusStatus status = controller.CheckHealth("busy-queue");

        status.Should().Be(BusStatus.Degraded);
    }

    [Fact]
    public void GetOrCreateManager_NullEndpointName_ThrowsArgumentNullException()
    {
        var controller = CreateController();
        var options = OptionsWithLimit();

        Action act = () => controller.GetOrCreateManager(null!, options);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CheckHealth_NoManagers_ReturnsHealthy()
    {
        // A freshly created controller with no registered endpoints must report Healthy
        // for any queried name (unknown endpoints default to healthy — ADR-004).
        var controller = CreateController();

        BusStatus status = controller.CheckHealth("nonexistent-queue");

        status.Should().Be(BusStatus.Healthy);
    }
}
