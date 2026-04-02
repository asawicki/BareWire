using System.Buffers;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using BareWire.Abstractions;
using BareWire.Abstractions.Exceptions;
using BareWire.Abstractions.Observability;
using BareWire.Abstractions.Pipeline;
using BareWire.Abstractions.Serialization;
using BareWire.Abstractions.Transport;
using BareWire;
using BareWire.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace BareWire.UnitTests.Core.Pipeline;

public sealed class MessagePipelineTests
{
    private sealed record TestMessage(string Value);

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static InboundMessage CreateInboundMessage(
        string messageId = "test-id",
        IReadOnlyDictionary<string, string>? headers = null)
        => new(
            messageId: messageId,
            headers: headers ?? new Dictionary<string, string>(),
            body: ReadOnlySequence<byte>.Empty,
            deliveryTag: 1UL);

    private static InboundMessage CreateInboundMessageWithBody(byte[] bodyBytes)
    {
        byte[] pooled = new byte[bodyBytes.Length];
        bodyBytes.CopyTo(pooled, 0);
        var sequence = new ReadOnlySequence<byte>(pooled);
        return new InboundMessage(
            messageId: "test-id",
            headers: new Dictionary<string, string>(),
            body: sequence,
            deliveryTag: 1UL);
    }

    private static (
        MessagePipeline Pipeline,
        MiddlewareChain Chain,
        ITransportAdapter Adapter,
        IServiceProvider ServiceProvider,
        IBareWireInstrumentation Instrumentation)
        CreatePipelineWithInstrumentation(IReadOnlyList<IMessageMiddleware>? middlewares = null)
    {
        ITransportAdapter adapter = Substitute.For<ITransportAdapter>();
        adapter.SettleAsync(Arg.Any<SettlementAction>(), Arg.Any<InboundMessage>(), Arg.Any<CancellationToken>())
               .Returns(Task.CompletedTask);

        IServiceProvider serviceProvider = Substitute.For<IServiceProvider>();
        IDeserializerResolver deserializerResolver = Substitute.For<IDeserializerResolver>();
        IBareWireInstrumentation instrumentation = Substitute.For<IBareWireInstrumentation>();

        var chain = new MiddlewareChain(middlewares ?? []);
        var pipeline = new MessagePipeline(chain, deserializerResolver, NullLogger<MessagePipeline>.Instance, instrumentation);

        return (pipeline, chain, adapter, serviceProvider, instrumentation);
    }

    private static (
        MessagePipeline Pipeline,
        MiddlewareChain Chain,
        ITransportAdapter Adapter,
        IServiceProvider ServiceProvider)
        CreatePipeline(IReadOnlyList<IMessageMiddleware>? middlewares = null)
    {
        var (pipeline, chain, adapter, serviceProvider, _) = CreatePipelineWithInstrumentation(middlewares);
        return (pipeline, chain, adapter, serviceProvider);
    }

    // ── Constructor null guards (MUT-789 to 792) ─────────────────────────────

    [Fact]
    public void Constructor_NullMiddlewareChain_ThrowsArgumentNullException()
    {
        IDeserializerResolver deserializerResolver = Substitute.For<IDeserializerResolver>();
        IBareWireInstrumentation instrumentation = Substitute.For<IBareWireInstrumentation>();

        var act = () => new MessagePipeline(null!, deserializerResolver, NullLogger<MessagePipeline>.Instance, instrumentation);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("middlewareChain");
    }

    [Fact]
    public void Constructor_NullDeserializerResolver_ThrowsArgumentNullException()
    {
        var chain = new MiddlewareChain([]);
        IBareWireInstrumentation instrumentation = Substitute.For<IBareWireInstrumentation>();

        var act = () => new MessagePipeline(chain, null!, NullLogger<MessagePipeline>.Instance, instrumentation);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("deserializerResolver");
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var chain = new MiddlewareChain([]);
        IDeserializerResolver deserializerResolver = Substitute.For<IDeserializerResolver>();
        IBareWireInstrumentation instrumentation = Substitute.For<IBareWireInstrumentation>();

        var act = () => new MessagePipeline(chain, deserializerResolver, null!, instrumentation);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_NullInstrumentation_ThrowsArgumentNullException()
    {
        var chain = new MiddlewareChain([]);
        IDeserializerResolver deserializerResolver = Substitute.For<IDeserializerResolver>();

        var act = () => new MessagePipeline(chain, deserializerResolver, NullLogger<MessagePipeline>.Instance, null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("instrumentation");
    }

    // ── ProcessInboundAsync argument guards (MUT-794, 795, 796) ─────────────

    [Fact]
    public async Task ProcessInboundAsync_NullMessage_ThrowsArgumentNullException()
    {
        var (pipeline, _, adapter, serviceProvider) = CreatePipeline();

        var act = async () => await pipeline.ProcessInboundAsync(null!, adapter, serviceProvider, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("message");
    }

    [Fact]
    public async Task ProcessInboundAsync_NullAdapter_ThrowsArgumentNullException()
    {
        var (pipeline, _, _, serviceProvider) = CreatePipeline();
        InboundMessage message = CreateInboundMessage();

        var act = async () => await pipeline.ProcessInboundAsync(message, null!, serviceProvider, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("adapter");
    }

    [Fact]
    public async Task ProcessInboundAsync_NullServiceProvider_ThrowsArgumentNullException()
    {
        var (pipeline, _, adapter, _) = CreatePipeline();
        InboundMessage message = CreateInboundMessage();

        var act = async () => await pipeline.ProcessInboundAsync(message, adapter, null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("serviceProvider");
    }

    // ── MessageType fallback to "unknown" (MUT-798) ───────────────────────────

    [Fact]
    public async Task ProcessInboundAsync_NoMessageTypeHeader_RecordsInflightWithUnknownType()
    {
        var (pipeline, _, adapter, serviceProvider, instrumentation) = CreatePipelineWithInstrumentation();
        // Headers have no BW-MessageType — msgType will be null, so messageType must default to "unknown"
        InboundMessage message = new(
            messageId: "test-id",
            headers: new Dictionary<string, string>(),
            body: ReadOnlySequence<byte>.Empty,
            deliveryTag: 1UL);

        await pipeline.ProcessInboundAsync(message, adapter, serviceProvider, CancellationToken.None);

        instrumentation.Received().RecordInflight(Arg.Any<string>(), "unknown", +1, Arg.Any<int>());
    }

    [Fact]
    public async Task ProcessInboundAsync_WithMessageTypeHeader_RecordsInflightWithActualType()
    {
        var (pipeline, _, adapter, serviceProvider, instrumentation) = CreatePipelineWithInstrumentation();
        InboundMessage message = new(
            messageId: "test-id",
            headers: new Dictionary<string, string> { ["BW-MessageType"] = "OrderCreated" },
            body: ReadOnlySequence<byte>.Empty,
            deliveryTag: 1UL);

        await pipeline.ProcessInboundAsync(message, adapter, serviceProvider, CancellationToken.None);

        instrumentation.Received().RecordInflight(Arg.Any<string>(), "OrderCreated", +1, Arg.Any<int>());
    }

    // ── Body length (MUT-804, 805, 806) ─────────────────────────────────────

    [Fact]
    public async Task ProcessInboundAsync_WithBody_RecordsInflightWithPositiveBodyLength()
    {
        var (pipeline, _, adapter, serviceProvider, instrumentation) = CreatePipelineWithInstrumentation();
        byte[] bodyBytes = [1, 2, 3, 4, 5]; // 5 bytes
        InboundMessage message = CreateInboundMessageWithBody(bodyBytes);

        await pipeline.ProcessInboundAsync(message, adapter, serviceProvider, CancellationToken.None);

        instrumentation.Received().RecordInflight(Arg.Any<string>(), Arg.Any<string>(), +1, 5);
    }

    [Fact]
    public async Task ProcessInboundAsync_WithBody_RecordsConsumeWithPositiveBodyLength()
    {
        var (pipeline, _, adapter, serviceProvider, instrumentation) = CreatePipelineWithInstrumentation();
        byte[] bodyBytes = [10, 20, 30]; // 3 bytes
        InboundMessage message = CreateInboundMessageWithBody(bodyBytes);

        await pipeline.ProcessInboundAsync(message, adapter, serviceProvider, CancellationToken.None);

        instrumentation.Received().RecordConsume(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<double>(), 3);
    }

    [Fact]
    public async Task ProcessInboundAsync_WithBody_BodyLengthIsPositiveNotNegative()
    {
        var (pipeline, _, adapter, serviceProvider, instrumentation) = CreatePipelineWithInstrumentation();
        byte[] bodyBytes = new byte[100];
        InboundMessage message = CreateInboundMessageWithBody(bodyBytes);

        await pipeline.ProcessInboundAsync(message, adapter, serviceProvider, CancellationToken.None);

        // Capture the bytesDelta argument for the +1 inflight call to ensure it is positive
        int capturedBodyLength = 0;
        instrumentation.RecordInflight(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<int>(d => d == +1),
            Arg.Do<int>(b => capturedBodyLength = b));
        await pipeline.ProcessInboundAsync(message, adapter, serviceProvider, CancellationToken.None);

        capturedBodyLength.Should().BeGreaterThan(0);
    }

    // ── Settlement flow (MUT-812, 815, 817-822) ───────────────────────────────

    [Fact]
    public async Task ProcessInboundAsync_SuccessfulConsume_ReturnsAck()
    {
        var (pipeline, _, adapter, serviceProvider) = CreatePipeline();
        InboundMessage message = CreateInboundMessage();

        SettlementAction result = await pipeline.ProcessInboundAsync(message, adapter, serviceProvider, CancellationToken.None);

        result.Should().Be(SettlementAction.Ack);
        await adapter.Received(1).SettleAsync(SettlementAction.Ack, message, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessInboundAsync_UnknownPayload_ReturnsReject()
    {
        IMessageMiddleware throwingMiddleware = Substitute.For<IMessageMiddleware>();
        throwingMiddleware.InvokeAsync(Arg.Any<MessageContext>(), Arg.Any<NextMiddleware>())
            .Returns<Task>(_ => throw new UnknownPayloadException("test-endpoint", "application/unknown"));

        var (pipeline, _, adapter, serviceProvider) = CreatePipeline([throwingMiddleware]);
        InboundMessage message = CreateInboundMessage();

        SettlementAction result = await pipeline.ProcessInboundAsync(message, adapter, serviceProvider, CancellationToken.None);

        result.Should().Be(SettlementAction.Reject);
        await adapter.Received(1).SettleAsync(SettlementAction.Reject, message, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessInboundAsync_UnknownPayload_RecordsFailure()
    {
        IMessageMiddleware throwingMiddleware = Substitute.For<IMessageMiddleware>();
        throwingMiddleware.InvokeAsync(Arg.Any<MessageContext>(), Arg.Any<NextMiddleware>())
            .Returns<Task>(_ => throw new UnknownPayloadException("test-endpoint", "application/unknown"));

        var (pipeline, _, adapter, serviceProvider, instrumentation) = CreatePipelineWithInstrumentation([throwingMiddleware]);
        InboundMessage message = CreateInboundMessage();

        await pipeline.ProcessInboundAsync(message, adapter, serviceProvider, CancellationToken.None);

        instrumentation.Received(1).RecordFailure(
            Arg.Any<string>(),
            Arg.Any<string>(),
            nameof(UnknownPayloadException));
    }

    [Fact]
    public async Task ProcessInboundAsync_ConsumerThrows_ReturnsNack()
    {
        IMessageMiddleware throwingMiddleware = Substitute.For<IMessageMiddleware>();
        throwingMiddleware.InvokeAsync(Arg.Any<MessageContext>(), Arg.Any<NextMiddleware>())
            .Returns<Task>(_ => throw new InvalidOperationException("consumer failed"));

        var (pipeline, _, adapter, serviceProvider) = CreatePipeline([throwingMiddleware]);
        InboundMessage message = CreateInboundMessage();

        SettlementAction result = await pipeline.ProcessInboundAsync(message, adapter, serviceProvider, CancellationToken.None);

        result.Should().Be(SettlementAction.Nack);
        await adapter.Received(1).SettleAsync(SettlementAction.Nack, message, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessInboundAsync_ConsumerThrows_RecordsFailure()
    {
        IMessageMiddleware throwingMiddleware = Substitute.For<IMessageMiddleware>();
        throwingMiddleware.InvokeAsync(Arg.Any<MessageContext>(), Arg.Any<NextMiddleware>())
            .Returns<Task>(_ => throw new InvalidOperationException("consumer failed"));

        var (pipeline, _, adapter, serviceProvider, instrumentation) = CreatePipelineWithInstrumentation([throwingMiddleware]);
        InboundMessage message = CreateInboundMessage();

        await pipeline.ProcessInboundAsync(message, adapter, serviceProvider, CancellationToken.None);

        instrumentation.Received(1).RecordFailure(
            Arg.Any<string>(),
            Arg.Any<string>(),
            nameof(InvalidOperationException));
    }

    [Fact]
    public async Task ProcessInboundAsync_SuccessfulConsume_RecordsConsumeInFinally()
    {
        var (pipeline, _, adapter, serviceProvider, instrumentation) = CreatePipelineWithInstrumentation();
        InboundMessage message = CreateInboundMessage();

        await pipeline.ProcessInboundAsync(message, adapter, serviceProvider, CancellationToken.None);

        instrumentation.Received(1).RecordConsume(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<double>(),
            Arg.Any<int>());
    }

    [Fact]
    public async Task ProcessInboundAsync_ExceptionThrown_StillRecordsConsumeInFinally()
    {
        IMessageMiddleware throwingMiddleware = Substitute.For<IMessageMiddleware>();
        throwingMiddleware.InvokeAsync(Arg.Any<MessageContext>(), Arg.Any<NextMiddleware>())
            .Returns<Task>(_ => throw new InvalidOperationException("fail"));

        var (pipeline, _, adapter, serviceProvider, instrumentation) = CreatePipelineWithInstrumentation([throwingMiddleware]);
        InboundMessage message = CreateInboundMessage();

        await pipeline.ProcessInboundAsync(message, adapter, serviceProvider, CancellationToken.None);

        instrumentation.Received(1).RecordConsume(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<double>(),
            Arg.Any<int>());
    }

    [Fact]
    public async Task ProcessInboundAsync_SuccessfulConsume_RecordsInflightDecrementInFinally()
    {
        var (pipeline, _, adapter, serviceProvider, instrumentation) = CreatePipelineWithInstrumentation();
        InboundMessage message = CreateInboundMessage();

        await pipeline.ProcessInboundAsync(message, adapter, serviceProvider, CancellationToken.None);

        // +1 at entry, -1 in finally
        instrumentation.Received(1).RecordInflight(Arg.Any<string>(), Arg.Any<string>(), +1, Arg.Any<int>());
        instrumentation.Received(1).RecordInflight(Arg.Any<string>(), Arg.Any<string>(), -1, Arg.Any<int>());
    }

    [Fact]
    public async Task ProcessInboundAsync_ExceptionThrown_StillRecordsInflightDecrementInFinally()
    {
        IMessageMiddleware throwingMiddleware = Substitute.For<IMessageMiddleware>();
        throwingMiddleware.InvokeAsync(Arg.Any<MessageContext>(), Arg.Any<NextMiddleware>())
            .Returns<Task>(_ => throw new InvalidOperationException("fail"));

        var (pipeline, _, adapter, serviceProvider, instrumentation) = CreatePipelineWithInstrumentation([throwingMiddleware]);
        InboundMessage message = CreateInboundMessage();

        await pipeline.ProcessInboundAsync(message, adapter, serviceProvider, CancellationToken.None);

        instrumentation.Received(1).RecordInflight(Arg.Any<string>(), Arg.Any<string>(), +1, Arg.Any<int>());
        instrumentation.Received(1).RecordInflight(Arg.Any<string>(), Arg.Any<string>(), -1, Arg.Any<int>());
    }

    [Fact]
    public async Task ProcessInboundAsync_WithBody_InflightDecrementUsesNegativeBodyLength()
    {
        var (pipeline, _, adapter, serviceProvider, instrumentation) = CreatePipelineWithInstrumentation();
        byte[] bodyBytes = [1, 2, 3, 4, 5]; // 5 bytes
        InboundMessage message = CreateInboundMessageWithBody(bodyBytes);

        await pipeline.ProcessInboundAsync(message, adapter, serviceProvider, CancellationToken.None);

        // The decrement call should have bytesDelta = -5 (negative of 5-byte body)
        instrumentation.Received(1).RecordInflight(Arg.Any<string>(), Arg.Any<string>(), -1, -5);
    }

    [Fact]
    public async Task ProcessInboundAsync_ExecutesMiddlewareChain()
    {
        var executionOrder = new List<string>();

        IMessageMiddleware first = Substitute.For<IMessageMiddleware>();
        first.InvokeAsync(Arg.Any<MessageContext>(), Arg.Any<NextMiddleware>())
            .Returns(async callInfo =>
            {
                executionOrder.Add("first");
                await callInfo.ArgAt<NextMiddleware>(1)(callInfo.ArgAt<MessageContext>(0));
            });

        IMessageMiddleware second = Substitute.For<IMessageMiddleware>();
        second.InvokeAsync(Arg.Any<MessageContext>(), Arg.Any<NextMiddleware>())
            .Returns(async callInfo =>
            {
                executionOrder.Add("second");
                await callInfo.ArgAt<NextMiddleware>(1)(callInfo.ArgAt<MessageContext>(0));
            });

        var (pipeline, _, adapter, serviceProvider) = CreatePipeline([first, second]);
        InboundMessage message = CreateInboundMessage();

        await pipeline.ProcessInboundAsync(message, adapter, serviceProvider, CancellationToken.None);

        executionOrder.Should().Equal("first", "second");
    }

    // ── ProcessOutboundAsync null guards (MUT-826, 827, 828) ─────────────────

    [Fact]
    public void ProcessOutboundAsync_NullMessage_ThrowsArgumentNullException()
    {
        IMessageSerializer serializer = Substitute.For<IMessageSerializer>();
        serializer.ContentType.Returns("application/json");

        var act = () => MessagePipeline.ProcessOutboundAsync<TestMessage>(null!, serializer, "routing.key", null, CancellationToken.None);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("message");
    }

    [Fact]
    public void ProcessOutboundAsync_NullSerializer_ThrowsArgumentNullException()
    {
        TestMessage message = new("hello");

        var act = () => MessagePipeline.ProcessOutboundAsync(message, null!, "routing.key", null, CancellationToken.None);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("serializer");
    }

    [Fact]
    public void ProcessOutboundAsync_NullRoutingKey_ThrowsArgumentNullException()
    {
        TestMessage message = new("hello");
        IMessageSerializer serializer = Substitute.For<IMessageSerializer>();
        serializer.ContentType.Returns("application/json");

        var act = () => MessagePipeline.ProcessOutboundAsync(message, serializer, null!, null, CancellationToken.None);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("routingKey");
    }

    // ── ProcessOutboundAsync null headers fallback (MUT-832) ─────────────────

    [Fact]
    public void ProcessOutboundAsync_NullHeaders_OutboundMessageHasEmptyHeaders()
    {
        TestMessage message = new("hello");
        IMessageSerializer serializer = Substitute.For<IMessageSerializer>();
        serializer.ContentType.Returns("application/json");

        OutboundMessage outbound = MessagePipeline.ProcessOutboundAsync(message, serializer, "routing.key", null, CancellationToken.None);

        outbound.Headers.Should().NotBeNull();
        outbound.Headers.Should().BeEmpty();
    }

    [Fact]
    public void ProcessOutboundAsync_WithHeaders_OutboundMessageRetainsHeaders()
    {
        TestMessage message = new("hello");
        IMessageSerializer serializer = Substitute.For<IMessageSerializer>();
        serializer.ContentType.Returns("application/json");
        var headers = new Dictionary<string, string> { ["x-custom"] = "value" };

        OutboundMessage outbound = MessagePipeline.ProcessOutboundAsync(message, serializer, "routing.key", headers, CancellationToken.None);

        outbound.Headers.Should().ContainKey("x-custom").WhoseValue.Should().Be("value");
    }

    [Fact]
    public void ProcessOutboundAsync_SerializesAndSends()
    {
        IMessageSerializer serializer = Substitute.For<IMessageSerializer>();
        serializer.ContentType.Returns("application/json");
        serializer.When(s => s.Serialize(Arg.Any<TestMessage>(), Arg.Any<IBufferWriter<byte>>()))
                  .Do(_ => { /* no-op: zero bytes written is fine for this test */ });

        TestMessage message = new("hello");
        const string routingKey = "my.routing.key";
        var extraHeaders = new Dictionary<string, string> { ["x-trace"] = "abc" };

        OutboundMessage outbound = MessagePipeline.ProcessOutboundAsync(message, serializer, routingKey, extraHeaders, CancellationToken.None);

        outbound.RoutingKey.Should().Be(routingKey);
        outbound.ContentType.Should().Be("application/json");
        outbound.Headers.Should().ContainKey("x-trace").WhoseValue.Should().Be("abc");
    }

    [Fact]
    public void ProcessOutboundAsync_UsesPooledBufferWriter()
    {
        IBufferWriter<byte>? capturedWriter = null;
        IMessageSerializer serializer = Substitute.For<IMessageSerializer>();
        serializer.ContentType.Returns("application/json");
        serializer.When(s => s.Serialize(Arg.Any<TestMessage>(), Arg.Any<IBufferWriter<byte>>()))
                  .Do(callInfo => capturedWriter = callInfo.ArgAt<IBufferWriter<byte>>(1));

        MessagePipeline.ProcessOutboundAsync(new TestMessage("value"), serializer, "key", null, CancellationToken.None);

        capturedWriter.Should().NotBeNull();
        capturedWriter.Should().BeAssignableTo<IBufferWriter<byte>>();
    }

    // ── ParseOrHashMessageId (MUT-834) ────────────────────────────────────────

    [Fact]
    public async Task ProcessInboundAsync_ValidGuidMessageId_PreservesGuid()
    {
        Guid expectedGuid = Guid.NewGuid();
        string guidString = expectedGuid.ToString();
        var (pipeline, _, adapter, serviceProvider, instrumentation) = CreatePipelineWithInstrumentation();

        // Capture the messageId passed to StartConsumeActivity which receives the parsed Guid
        Guid capturedMessageId = Guid.Empty;
        instrumentation.StartConsumeActivity(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Do<Guid>(id => capturedMessageId = id),
            Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns(_ => (Activity?)null);

        InboundMessage message = new(
            messageId: guidString,
            headers: new Dictionary<string, string>(),
            body: ReadOnlySequence<byte>.Empty,
            deliveryTag: 1UL);

        await pipeline.ProcessInboundAsync(message, adapter, serviceProvider, CancellationToken.None);

        capturedMessageId.Should().Be(expectedGuid);
    }

    [Fact]
    public async Task ProcessInboundAsync_NonGuidMessageId_ProducesDeterministicHashGuid()
    {
        const string nonGuidId = "not-a-guid-at-all";
        var (pipeline, _, adapter, serviceProvider, instrumentation) = CreatePipelineWithInstrumentation();

        Guid firstCapturedId = Guid.Empty;
        instrumentation.StartConsumeActivity(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Do<Guid>(id => firstCapturedId = id),
            Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns(_ => (Activity?)null);

        InboundMessage message = new(
            messageId: nonGuidId,
            headers: new Dictionary<string, string>(),
            body: ReadOnlySequence<byte>.Empty,
            deliveryTag: 1UL);

        await pipeline.ProcessInboundAsync(message, adapter, serviceProvider, CancellationToken.None);

        // The result must not be Guid.Empty (i.e., a real hash was computed)
        firstCapturedId.Should().NotBe(Guid.Empty);

        // Process again with the same ID — the result must be deterministic (same Guid)
        Guid secondCapturedId = Guid.Empty;
        instrumentation.StartConsumeActivity(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Do<Guid>(id => secondCapturedId = id),
            Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns(_ => (Activity?)null);

        await pipeline.ProcessInboundAsync(message, adapter, serviceProvider, CancellationToken.None);

        secondCapturedId.Should().Be(firstCapturedId);
    }

    [Fact]
    public async Task ProcessInboundAsync_NonGuidMessageId_GuidMatchesSha256Hash()
    {
        const string nonGuidId = "order-12345";
        var (pipeline, _, adapter, serviceProvider, instrumentation) = CreatePipelineWithInstrumentation();

        Guid capturedId = Guid.Empty;
        instrumentation.StartConsumeActivity(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Do<Guid>(id => capturedId = id),
            Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns(_ => (Activity?)null);

        InboundMessage message = new(
            messageId: nonGuidId,
            headers: new Dictionary<string, string>(),
            body: ReadOnlySequence<byte>.Empty,
            deliveryTag: 1UL);

        await pipeline.ProcessInboundAsync(message, adapter, serviceProvider, CancellationToken.None);

        // Compute the expected Guid the same way the production code does
        byte[] idBytes = Encoding.UTF8.GetBytes(nonGuidId);
        byte[] hash = SHA256.HashData(idBytes);
        Guid expectedGuid = new(hash.AsSpan(0, 16));

        capturedId.Should().Be(expectedGuid);
    }

    [Fact]
    public async Task ProcessInboundAsync_ValidGuidMessageId_NotReplacedByHash()
    {
        Guid knownGuid = new Guid("12345678-1234-1234-1234-123456789abc");
        var (pipeline, _, adapter, serviceProvider, instrumentation) = CreatePipelineWithInstrumentation();

        Guid capturedId = Guid.Empty;
        instrumentation.StartConsumeActivity(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Do<Guid>(id => capturedId = id),
            Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns(_ => (Activity?)null);

        InboundMessage message = new(
            messageId: knownGuid.ToString(),
            headers: new Dictionary<string, string>(),
            body: ReadOnlySequence<byte>.Empty,
            deliveryTag: 1UL);

        await pipeline.ProcessInboundAsync(message, adapter, serviceProvider, CancellationToken.None);

        // Must return the parsed Guid, NOT a hash of it
        capturedId.Should().Be(knownGuid);

        // Verify it's not a hash of the string (which would be different)
        byte[] idBytes = Encoding.UTF8.GetBytes(knownGuid.ToString());
        byte[] hash = SHA256.HashData(idBytes);
        Guid hashBasedGuid = new(hash.AsSpan(0, 16));
        capturedId.Should().NotBe(hashBasedGuid);
    }
}
