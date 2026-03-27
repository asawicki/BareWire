using System.Buffers;
using System.Threading.Channels;
using AwesomeAssertions;
using BareWire.Abstractions;
using BareWire.Abstractions.Transport;
using BareWire.FlowControl;
using Microsoft.Extensions.Logging.Abstractions;

namespace BareWire.UnitTests.Core.FlowControl;

public sealed class EndpointPipelineTests
{
    private static FlowControlOptions DefaultOptions(
        int capacity = 10,
        BoundedChannelFullMode fullMode = BoundedChannelFullMode.Wait) =>
        new()
        {
            InternalQueueCapacity = capacity,
            FullMode = fullMode,
            MaxInFlightMessages = 100,
            MaxInFlightBytes = 64 * 1024 * 1024,
        };

    private static InboundMessage CreateMessage(string id = "msg-1", byte[]? body = null)
    {
        body ??= [1, 2, 3];
        return new InboundMessage(
            id,
            new Dictionary<string, string>(),
            new ReadOnlySequence<byte>(body),
            deliveryTag: 1);
    }

    private static EndpointPipeline CreatePipeline(FlowControlOptions? options = null) =>
        new("test-endpoint", options ?? DefaultOptions(), NullLogger<EndpointPipeline>.Instance);

    [Fact]
    public async Task EnqueueAsync_WritesToChannel()
    {
        await using EndpointPipeline pipeline = CreatePipeline();
        InboundMessage message = CreateMessage();

        await pipeline.EnqueueAsync(message, CancellationToken.None);

        pipeline.GetReader().TryRead(out _).Should().BeTrue();
    }

    [Fact]
    public async Task DequeueAsync_ReadsFromChannel()
    {
        await using EndpointPipeline pipeline = CreatePipeline();
        InboundMessage original = CreateMessage("dequeue-test", [10, 20, 30]);

        await pipeline.EnqueueAsync(original, CancellationToken.None);
        InboundMessage dequeued = await pipeline.DequeueAsync(CancellationToken.None);

        dequeued.MessageId.Should().Be("dequeue-test");
        dequeued.Body.Length.Should().Be(3);
    }

    [Fact]
    public async Task EnqueueAsync_WhenChannelFull_WaitsForSpace()
    {
        // Capacity of 1, FullMode.Wait — second enqueue must block until first is dequeued.
        var options = DefaultOptions(capacity: 1, fullMode: BoundedChannelFullMode.Wait);
        await using EndpointPipeline pipeline = CreatePipeline(options);

        // Fill the single slot.
        await pipeline.EnqueueAsync(CreateMessage("first"), CancellationToken.None);

        // Second enqueue should not complete within a very short window because the channel is full.
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        Func<Task> blockedEnqueue = async () =>
            await pipeline.EnqueueAsync(CreateMessage("second"), cts.Token).ConfigureAwait(false);

        await blockedEnqueue.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SettleMessage_ReleasesInflightCredits()
    {
        await using EndpointPipeline pipeline = CreatePipeline();
        InboundMessage message = CreateMessage(body: [1, 2, 3, 4, 5]);

        await pipeline.EnqueueAsync(message, CancellationToken.None);
        InboundMessage dequeued = await pipeline.DequeueAsync(CancellationToken.None);

        int countBefore = pipeline.CreditManager.InflightCount;

        pipeline.SettleMessage(dequeued, dequeued.Body.Length);

        int countAfter = pipeline.CreditManager.InflightCount;

        countBefore.Should().Be(1);
        countAfter.Should().Be(0);
    }

    [Fact]
    public async Task CreditManager_TracksInflightCorrectly()
    {
        await using EndpointPipeline pipeline = CreatePipeline();

        pipeline.CreditManager.InflightCount.Should().Be(0);

        await pipeline.EnqueueAsync(CreateMessage("a", [1, 2, 3]), CancellationToken.None);
        await pipeline.EnqueueAsync(CreateMessage("b", [4, 5, 6]), CancellationToken.None);

        pipeline.CreditManager.InflightCount.Should().Be(2);
        pipeline.CreditManager.InflightBytes.Should().Be(6);
    }

    [Fact]
    public async Task SettleMessage_ReleasesInflight_CountAndBytes()
    {
        await using EndpointPipeline pipeline = CreatePipeline();
        byte[] body = [0xAA, 0xBB, 0xCC, 0xDD, 0xEE];
        InboundMessage message = CreateMessage("settle-test", body);

        await pipeline.EnqueueAsync(message, CancellationToken.None);
        InboundMessage dequeued = await pipeline.DequeueAsync(CancellationToken.None);

        int countBefore = pipeline.CreditManager.InflightCount;
        long bytesBefore = pipeline.CreditManager.InflightBytes;

        pipeline.SettleMessage(dequeued, dequeued.Body.Length);

        pipeline.CreditManager.InflightCount.Should().Be(countBefore - 1);
        pipeline.CreditManager.InflightBytes.Should().Be(bytesBefore - body.Length);
    }

    [Fact]
    public async Task EnqueueAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Capacity of 1, FullMode.Wait — fill the slot first, then use a pre-cancelled
        // token so the second enqueue fails immediately without any blocking.
        var options = DefaultOptions(capacity: 1, fullMode: BoundedChannelFullMode.Wait);
        await using EndpointPipeline pipeline = CreatePipeline(options);

        await pipeline.EnqueueAsync(CreateMessage("blocker"), CancellationToken.None);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = async () =>
            await pipeline.EnqueueAsync(CreateMessage("cancelled"), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task DisposeAsync_CompletesChannel()
    {
        EndpointPipeline pipeline = CreatePipeline();

        await pipeline.DisposeAsync();

        // After dispose the writer is completed; waiting to read should resolve to false
        // immediately (no more data will ever arrive).
        bool hasMore = await pipeline.GetReader().WaitToReadAsync(CancellationToken.None);

        hasMore.Should().BeFalse();
    }
}
