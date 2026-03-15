using AwesomeAssertions;
using BareWire.Abstractions;
using BareWire.Abstractions.Transport;
using BareWire.Testing;

namespace BareWire.UnitTests.Testing;

public sealed class BareWireTestHarnessTests
{
    private sealed record OrderPlaced(string OrderId);
    private sealed record PaymentRequested(decimal Amount);

    [Fact]
    public async Task CreateAsync_ReturnsWorkingBus()
    {
        await using BareWireTestHarness harness = await BareWireTestHarness.CreateAsync();

        harness.Bus.Should().NotBeNull();
        harness.Bus.BusId.Should().NotBe(Guid.Empty);
        harness.Bus.Address.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAsync_BusAddressReflectsInMemoryTransport()
    {
        await using BareWireTestHarness harness = await BareWireTestHarness.CreateAsync();

        harness.Bus.Address.Scheme.Should().Be("barewire");
        harness.Bus.Address.Host.Should().Be("inmemory");
    }

    [Fact]
    public async Task DisposeAsync_StopsBus()
    {
        BareWireTestHarness harness = await BareWireTestHarness.CreateAsync();

        // Bus should be working before disposal.
        harness.Bus.Should().NotBeNull();

        // Dispose should complete without throwing.
        Func<Task> dispose = async () => await harness.DisposeAsync();
        await dispose.Should().NotThrowAsync();
    }

    [Fact]
    public async Task WaitForPublishAsync_MessagePublished_ReturnsOutboundMessage()
    {
        await using BareWireTestHarness harness = await BareWireTestHarness.CreateAsync();

        Task<OutboundMessage> waitTask = harness.WaitForPublishAsync<OrderPlaced>(TimeSpan.FromSeconds(5));

        await harness.Bus.PublishAsync(new OrderPlaced("order-1"));

        OutboundMessage received = await waitTask;
        received.Should().NotBeNull();
        received.RoutingKey.Should().Contain(nameof(OrderPlaced));
    }

    [Fact]
    public async Task WaitForPublishAsync_Timeout_ThrowsTimeoutException()
    {
        await using BareWireTestHarness harness = await BareWireTestHarness.CreateAsync();

        // No message is published, so the wait should time out.
        Func<Task> act = () => harness.WaitForPublishAsync<OrderPlaced>(TimeSpan.FromMilliseconds(100));

        await act.Should().ThrowAsync<TimeoutException>();
    }

    [Fact]
    public async Task WaitForPublishAsync_WrongMessageType_DoesNotComplete()
    {
        await using BareWireTestHarness harness = await BareWireTestHarness.CreateAsync();

        Task<OutboundMessage> waitTask = harness.WaitForPublishAsync<OrderPlaced>(TimeSpan.FromMilliseconds(200));

        // Publish a different type — should not satisfy the wait for OrderPlaced.
        await harness.Bus.PublishAsync(new PaymentRequested(99.99m));

        Func<Task> act = () => waitTask;
        await act.Should().ThrowAsync<TimeoutException>();
    }

    [Fact]
    public async Task WaitForSendAsync_MessageSent_ReturnsOutboundMessage()
    {
        await using BareWireTestHarness harness = await BareWireTestHarness.CreateAsync();

        Task<OutboundMessage> waitTask = harness.WaitForSendAsync<OrderPlaced>(TimeSpan.FromSeconds(5));

        await harness.Bus.PublishAsync(new OrderPlaced("order-2"));

        OutboundMessage received = await waitTask;
        received.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAsync_WithNullConfigure_DoesNotThrow()
    {
        Func<Task> act = async () =>
        {
            await using BareWireTestHarness harness = await BareWireTestHarness.CreateAsync(configure: null);
        };

        await act.Should().NotThrowAsync();
    }
}
