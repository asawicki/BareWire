using System.Buffers;
using System.Text;
using System.Threading.Channels;
using AwesomeAssertions;
using BareWire.Abstractions;
using BareWire.Abstractions.Transport;
using BareWire.Testing;

namespace BareWire.UnitTests.Testing;

public sealed class InMemoryTransportAdapterTests
{
    private static OutboundMessage CreateOutbound(
        string routingKey,
        string body = "payload",
        string contentType = "application/json")
    {
        byte[] bytes = Encoding.UTF8.GetBytes(body);
        return new OutboundMessage(
            routingKey: routingKey,
            headers: new Dictionary<string, string>(),
            body: new ReadOnlyMemory<byte>(bytes),
            contentType: contentType);
    }

    private static FlowControlOptions DefaultFlowControl(int capacity = 1_000) =>
        new() { InternalQueueCapacity = capacity, FullMode = BoundedChannelFullMode.Wait };

    [Fact]
    public void TransportName_ReturnsInMemory()
    {
        var adapter = new InMemoryTransportAdapter();
        adapter.TransportName.Should().Be("InMemory");
    }

    [Fact]
    public void Capabilities_ReturnsNone()
    {
        var adapter = new InMemoryTransportAdapter();
        adapter.Capabilities.Should().Be(TransportCapabilities.None);
    }

    [Fact]
    public async Task SendBatchAsync_WritesToEndpointChannel()
    {
        await using var adapter = new InMemoryTransportAdapter();
        OutboundMessage message = CreateOutbound("queue-a");

        IReadOnlyList<SendResult> results = await adapter.SendBatchAsync([message]);

        results.Should().HaveCount(1);
        results[0].IsConfirmed.Should().BeTrue();
        results[0].DeliveryTag.Should().BeGreaterThan(0UL);
    }

    [Fact]
    public async Task ConsumeAsync_ReadsFromEndpointChannel()
    {
        await using var adapter = new InMemoryTransportAdapter();
        await adapter.SendBatchAsync([CreateOutbound("queue-consume", "test-body")]);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        InboundMessage received = await adapter
            .ConsumeAsync("queue-consume", DefaultFlowControl(), cts.Token)
            .FirstAsync(cts.Token);

        received.MessageId.Should().NotBeNullOrEmpty();
        received.DeliveryTag.Should().BeGreaterThan(0UL);
    }

    [Fact]
    public async Task SendAndConsume_Roundtrip_PreservesPayload()
    {
        await using var adapter = new InMemoryTransportAdapter();
        const string endpointName = "roundtrip-queue";
        const string payloadText = "Hello, BareWire!";

        byte[] payloadBytes = Encoding.UTF8.GetBytes(payloadText);
        var headers = new Dictionary<string, string> { ["x-trace-id"] = "abc123" };

        await adapter.SendBatchAsync(
            [new OutboundMessage(endpointName, headers, new ReadOnlyMemory<byte>(payloadBytes), "text/plain")]);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        InboundMessage received = await adapter
            .ConsumeAsync(endpointName, DefaultFlowControl(), cts.Token)
            .FirstAsync(cts.Token);

        Encoding.UTF8.GetString(received.Body.ToArray()).Should().Be(payloadText);
        received.Headers.Should().ContainKey("x-trace-id").WhoseValue.Should().Be("abc123");
        received.MessageId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SendBatchAsync_MultipleEndpoints_RoutesCorrectly()
    {
        await using var adapter = new InMemoryTransportAdapter();

        OutboundMessage msgA = CreateOutbound("queue-a", "hello-a");
        OutboundMessage msgB = CreateOutbound("queue-b", "hello-b");

        await adapter.SendBatchAsync([msgA, msgB]);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        InboundMessage fromA = await adapter
            .ConsumeAsync("queue-a", DefaultFlowControl(), cts.Token)
            .FirstAsync(cts.Token);
        InboundMessage fromB = await adapter
            .ConsumeAsync("queue-b", DefaultFlowControl(), cts.Token)
            .FirstAsync(cts.Token);

        Encoding.UTF8.GetString(fromA.Body.ToArray()).Should().Be("hello-a");
        Encoding.UTF8.GetString(fromB.Body.ToArray()).Should().Be("hello-b");
    }

    [Fact]
    public async Task DisposeAsync_CompletesAllChannels()
    {
        var adapter = new InMemoryTransportAdapter();
        const string endpointName = "dispose-queue";

        await adapter.SendBatchAsync([CreateOutbound(endpointName)]);

        var consumedMessages = new List<InboundMessage>();
        Task consumeTask = Task.Run(async () =>
        {
            await foreach (InboundMessage msg in adapter.ConsumeAsync(endpointName, DefaultFlowControl()))
            {
                consumedMessages.Add(msg);
            }
        });

        await Task.Delay(50);
        await adapter.DisposeAsync();

        // ConsumeAsync should terminate gracefully after writer is completed.
        await consumeTask.WaitAsync(TimeSpan.FromSeconds(3));
        consumedMessages.Should().HaveCount(1);
    }
}
