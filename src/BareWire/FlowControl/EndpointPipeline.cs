using System.Buffers;
using System.Threading.Channels;
using BareWire.Abstractions;
using BareWire.Abstractions.Transport;
using Microsoft.Extensions.Logging;

namespace BareWire.FlowControl;

internal sealed class EndpointPipeline : IAsyncDisposable
{
    private readonly Channel<InboundMessage> _channel;
    private readonly CreditManager _creditManager;
    private readonly string _endpointName;
    private readonly ILogger<EndpointPipeline> _logger;

    internal EndpointPipeline(string endpointName, FlowControlOptions options, ILogger<EndpointPipeline> logger)
    {
        _endpointName = endpointName ?? throw new ArgumentNullException(nameof(endpointName));
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _creditManager = new CreditManager(options);
        _channel = Channel.CreateBounded<InboundMessage>(new BoundedChannelOptions(options.InternalQueueCapacity)
        {
            SingleWriter = false,
            SingleReader = true,
            FullMode = options.FullMode,
        });
    }

    internal CreditManager CreditManager => _creditManager;

    internal async ValueTask EnqueueAsync(InboundMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        // Copy body into owned ArrayPool memory to prevent use-after-free when
        // the transport reclaims its original buffer (ADR-003 / plan section 9).
        long bodyLength = message.Body.Length;
        int rentLength = (int)Math.Min(bodyLength, int.MaxValue);
        byte[] ownedBuffer = ArrayPool<byte>.Shared.Rent(rentLength);

        message.Body.CopyTo(ownedBuffer);

        var ownedMessage = new InboundMessage(
            message.MessageId,
            message.Headers,
            new ReadOnlySequence<byte>(ownedBuffer, 0, rentLength),
            message.DeliveryTag);

        await _channel.Writer.WriteAsync(ownedMessage, ct).ConfigureAwait(false);

        _creditManager.TrackInflight(1, bodyLength);
    }

    internal ValueTask<InboundMessage> DequeueAsync(CancellationToken ct = default)
    {
        return _channel.Reader.ReadAsync(ct);
    }

    internal ChannelReader<InboundMessage> GetReader() => _channel.Reader;

    internal void SettleMessage(InboundMessage message, long bodyLength)
    {
        ArgumentNullException.ThrowIfNull(message);

        _creditManager.ReleaseInflight(1, bodyLength);

        // Return the owned buffer rented in EnqueueAsync back to the pool.
        // The body's first segment array is the rented buffer.
        if (message.Body.First is { IsEmpty: false } segment &&
            System.Runtime.InteropServices.MemoryMarshal.TryGetArray(segment, out var arraySegment) &&
            arraySegment.Array is { } buffer)
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public ValueTask DisposeAsync()
    {
        _channel.Writer.Complete();
        return ValueTask.CompletedTask;
    }
}
