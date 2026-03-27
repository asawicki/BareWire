using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using AwesomeAssertions;
using BareWire.Abstractions;
using BareWire.Abstractions.Configuration;
using BareWire.Abstractions.Pipeline;
using BareWire.Abstractions.Serialization;
using BareWire.Abstractions.Transport;
using BareWire.Bus;
using BareWire.FlowControl;
using BareWire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace BareWire.UnitTests.Core.Bus;

// Must be public so ConsumerInvokerFactory.CreateRaw can build a delegate over this type
// and GetService<ThrowingRawConsumer>() can resolve it from the NSubstitute DI mock.
public sealed class ThrowingRawConsumer : IRawConsumer
{
    private int _callCount;

    /// <summary>Number of times <see cref="ConsumeAsync"/> was called (including retry attempts).</summary>
    public int CallCount => _callCount;

    public Task ConsumeAsync(RawConsumeContext context)
    {
        Interlocked.Increment(ref _callCount);
        throw new InvalidOperationException("Consumer failed intentionally.");
    }
}

// Must be public so ConsumerInvokerFactory.CreateRaw can build a delegate over this type.
public sealed class TrackingRawConsumer : IRawConsumer
{
    private readonly List<string> _executionLog;
    private readonly string _label;

    public TrackingRawConsumer(List<string> executionLog, string label)
    {
        _executionLog = executionLog;
        _label = label;
    }

    public Task ConsumeAsync(RawConsumeContext context)
    {
        _executionLog.Add(_label);
        return Task.CompletedTask;
    }
}

// Simple middleware that logs its label into a shared execution log for ordering verification.
public sealed class TrackingMiddleware(List<string> executionLog, string label) : IMessageMiddleware
{
    public async Task InvokeAsync(MessageContext context, NextMiddleware nextMiddleware)
    {
        executionLog.Add(label);
        await nextMiddleware(context).ConfigureAwait(false);
    }
}

/// <summary>
/// Tests that verify retry middleware is correctly wired into <see cref="ReceiveEndpointRunner"/>
/// via <see cref="EndpointBinding.RetryCount"/> and <see cref="EndpointBinding.RetryInterval"/>.
/// </summary>
public sealed class ReceiveEndpointRunnerTests
{
    private const string EndpointName = "retry-test-endpoint";

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (
        ReceiveEndpointRunner Runner,
        ThrowingRawConsumer Consumer,
        ChannelWriter<InboundMessage> MessageWriter,
        ITransportAdapter Adapter)
        CreateRunnerWithThrowingConsumer(int retryCount, TimeSpan retryInterval)
    {
        Channel<InboundMessage> channel = Channel.CreateBounded<InboundMessage>(
            new BoundedChannelOptions(64) { SingleWriter = false, SingleReader = true });

        ITransportAdapter adapter = Substitute.For<ITransportAdapter>();
        adapter.TransportName.Returns("test");
        adapter.ConsumeAsync(
                Arg.Any<string>(),
                Arg.Any<FlowControlOptions>(),
                Arg.Any<CancellationToken>())
               .Returns(callInfo => ReadChannelAsync(channel.Reader, callInfo.ArgAt<CancellationToken>(2)));
        adapter.SettleAsync(
                Arg.Any<SettlementAction>(),
                Arg.Any<InboundMessage>(),
                Arg.Any<CancellationToken>())
               .Returns(Task.CompletedTask);

        IMessageDeserializer deserializer = Substitute.For<IMessageDeserializer>();
        deserializer.ContentType.Returns("application/json");

        ThrowingRawConsumer consumer = new();

        IServiceScopeFactory scopeFactory = Substitute.For<IServiceScopeFactory>();
        IServiceScope scope = Substitute.For<IServiceScope>();
        IServiceProvider provider = Substitute.For<IServiceProvider>();

        scopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(provider);
        provider.GetService(typeof(ThrowingRawConsumer)).Returns(consumer);
        provider.GetService(typeof(IEnumerable<IMessageMiddleware>))
            .Returns(Array.Empty<IMessageMiddleware>());

        FlowController flowController = new(NullLogger<FlowController>.Instance);

        EndpointBinding binding = new()
        {
            EndpointName = EndpointName,
            PrefetchCount = 4,
            Consumers = [],
            RawConsumers = [typeof(ThrowingRawConsumer)],
            RetryCount = retryCount,
            RetryInterval = retryInterval,
        };

        // Pass a real ILoggerFactory so RetryMiddleware and DeadLetterMiddleware get loggers.
        ILoggerFactory loggerFactory = NullLoggerFactory.Instance;

        ReceiveEndpointRunner runner = new(
            binding,
            adapter,
            deserializer,
            Substitute.For<IPublishEndpoint>(),
            Substitute.For<ISendEndpointProvider>(),
            scopeFactory,
            flowController,
            new NullInstrumentation(),
            NullLogger<ReceiveEndpointRunner>.Instance,
            loggerFactory: loggerFactory);

        return (runner, consumer, channel.Writer, adapter);
    }

    private static InboundMessage MakeMessage(string id = "msg-1")
    {
        byte[] body = new byte[8];
        return new InboundMessage(
            messageId: id,
            headers: new Dictionary<string, string>(),
            body: new ReadOnlySequence<byte>(body),
            deliveryTag: 1UL);
    }

    private static async IAsyncEnumerable<InboundMessage> ReadChannelAsync(
        ChannelReader<InboundMessage> reader,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (InboundMessage msg in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return msg;
        }
    }

    private static (
        ReceiveEndpointRunner Runner,
        ThrowingRawConsumer Consumer,
        ChannelWriter<InboundMessage> MessageWriter,
        ITransportAdapter Adapter)
        CreateRunnerWithNullLoggerFactory(int retryCount)
    {
        Channel<InboundMessage> channel = Channel.CreateBounded<InboundMessage>(
            new BoundedChannelOptions(64) { SingleWriter = false, SingleReader = true });

        ITransportAdapter adapter = Substitute.For<ITransportAdapter>();
        adapter.TransportName.Returns("test");
        adapter.ConsumeAsync(
                Arg.Any<string>(),
                Arg.Any<FlowControlOptions>(),
                Arg.Any<CancellationToken>())
               .Returns(callInfo => ReadChannelAsync(channel.Reader, callInfo.ArgAt<CancellationToken>(2)));
        adapter.SettleAsync(
                Arg.Any<SettlementAction>(),
                Arg.Any<InboundMessage>(),
                Arg.Any<CancellationToken>())
               .Returns(Task.CompletedTask);

        IMessageDeserializer deserializer = Substitute.For<IMessageDeserializer>();
        deserializer.ContentType.Returns("application/json");

        ThrowingRawConsumer consumer = new();

        IServiceScopeFactory scopeFactory = Substitute.For<IServiceScopeFactory>();
        IServiceScope scope = Substitute.For<IServiceScope>();
        IServiceProvider provider = Substitute.For<IServiceProvider>();

        scopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(provider);
        provider.GetService(typeof(ThrowingRawConsumer)).Returns(consumer);
        provider.GetService(typeof(IEnumerable<IMessageMiddleware>))
            .Returns(Array.Empty<IMessageMiddleware>());

        FlowController flowController = new(NullLogger<FlowController>.Instance);

        EndpointBinding binding = new()
        {
            EndpointName = EndpointName,
            PrefetchCount = 4,
            Consumers = [],
            RawConsumers = [typeof(ThrowingRawConsumer)],
            RetryCount = retryCount,
            RetryInterval = TimeSpan.Zero,
        };

        // Deliberately pass null loggerFactory — this is the regression scenario for Bug 1.
        ReceiveEndpointRunner runner = new(
            binding,
            adapter,
            deserializer,
            Substitute.For<IPublishEndpoint>(),
            Substitute.For<ISendEndpointProvider>(),
            scopeFactory,
            flowController,
            new NullInstrumentation(),
            NullLogger<ReceiveEndpointRunner>.Instance,
            loggerFactory: null);

        return (runner, consumer, channel.Writer, adapter);
    }

    private static (
        ReceiveEndpointRunner Runner,
        ThrowingRawConsumer Consumer,
        ChannelWriter<InboundMessage> MessageWriter,
        ITransportAdapter Adapter,
        ILogger Logger)
        CreateRunnerWithDlxConfig(bool hasDeadLetterExchange)
    {
        Channel<InboundMessage> channel = Channel.CreateBounded<InboundMessage>(
            new BoundedChannelOptions(64) { SingleWriter = false, SingleReader = true });

        ITransportAdapter adapter = Substitute.For<ITransportAdapter>();
        adapter.TransportName.Returns("test");
        adapter.ConsumeAsync(
                Arg.Any<string>(),
                Arg.Any<FlowControlOptions>(),
                Arg.Any<CancellationToken>())
               .Returns(callInfo => ReadChannelAsync(channel.Reader, callInfo.ArgAt<CancellationToken>(2)));
        adapter.SettleAsync(
                Arg.Any<SettlementAction>(),
                Arg.Any<InboundMessage>(),
                Arg.Any<CancellationToken>())
               .Returns(Task.CompletedTask);

        IMessageDeserializer deserializer = Substitute.For<IMessageDeserializer>();
        deserializer.ContentType.Returns("application/json");

        ThrowingRawConsumer consumer = new();

        IServiceScopeFactory scopeFactory = Substitute.For<IServiceScopeFactory>();
        IServiceScope scope = Substitute.For<IServiceScope>();
        IServiceProvider provider = Substitute.For<IServiceProvider>();

        scopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(provider);
        provider.GetService(typeof(ThrowingRawConsumer)).Returns(consumer);
        provider.GetService(typeof(IEnumerable<IMessageMiddleware>))
            .Returns(Array.Empty<IMessageMiddleware>());

        FlowController flowController = new(NullLogger<FlowController>.Instance);

        EndpointBinding binding = new()
        {
            EndpointName = EndpointName,
            PrefetchCount = 4,
            Consumers = [],
            RawConsumers = [typeof(ThrowingRawConsumer)],
            RetryCount = 0,
            RetryInterval = TimeSpan.Zero,
            HasDeadLetterExchange = hasDeadLetterExchange,
        };

        ILogger logger = Substitute.For<ILogger>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        ReceiveEndpointRunner runner = new(
            binding,
            adapter,
            deserializer,
            Substitute.For<IPublishEndpoint>(),
            Substitute.For<ISendEndpointProvider>(),
            scopeFactory,
            flowController,
            new NullInstrumentation(),
            logger,
            loggerFactory: NullLoggerFactory.Instance);

        return (runner, consumer, channel.Writer, adapter, logger);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_ConsumerThrowsAndNoDlx_LogsWarning()
    {
        // Arrange
        var (runner, _, writer, _, logger) = CreateRunnerWithDlxConfig(hasDeadLetterExchange: false);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));

        await writer.WriteAsync(MakeMessage("msg-lost"), cts.Token);
        writer.Complete();

        // Act
        await runner.RunAsync(cts.Token);

        // Assert — warning about permanently lost message must be logged.
        IEnumerable<NSubstitute.Core.ICall> warningCalls = logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(ILogger.Log)
                && c.GetArguments()[0] is LogLevel level
                && level == LogLevel.Warning
                && c.GetArguments()[2]?.ToString()?.Contains("permanently lost") == true);
        warningCalls.Should().NotBeEmpty("expected a Warning log about 'permanently lost'");
    }

    [Fact]
    public async Task RunAsync_ConsumerThrowsWithDlx_DoesNotLogWarning()
    {
        // Arrange
        var (runner, _, writer, _, logger) = CreateRunnerWithDlxConfig(hasDeadLetterExchange: true);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));

        await writer.WriteAsync(MakeMessage("msg-dlx"), cts.Token);
        writer.Complete();

        // Act
        await runner.RunAsync(cts.Token);

        // Assert — no "permanently lost" warning when DLX is configured.
        IEnumerable<NSubstitute.Core.ICall> warningCalls = logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(ILogger.Log)
                && c.GetArguments()[0] is LogLevel level
                && level == LogLevel.Warning
                && c.GetArguments()[2]?.ToString()?.Contains("permanently lost") == true);
        warningCalls.Should().BeEmpty("should not warn about 'permanently lost' when DLX is configured");
    }

    [Fact]
    public async Task RunAsync_ConsumerThrowsWithRetry_RetriesBeforeNack()
    {
        // Arrange — RetryCount=2 means 1 original attempt + 2 retry attempts = 3 total calls.
        var (runner, consumer, writer, adapter) = CreateRunnerWithThrowingConsumer(
            retryCount: 2,
            retryInterval: TimeSpan.Zero);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));

        await writer.WriteAsync(MakeMessage("msg-retry"), cts.Token);
        writer.Complete();

        // Act
        await runner.RunAsync(cts.Token);

        // Assert — consumer was invoked 3 times (1 initial + 2 retries).
        consumer.CallCount.Should().Be(3);

        // After all retries are exhausted, the message must be NACKed.
        await adapter.Received(1).SettleAsync(
            SettlementAction.Nack,
            Arg.Is<InboundMessage>(m => m.MessageId == "msg-retry"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_NoRetryConfigured_ImmediateNackOnException()
    {
        // Arrange — RetryCount=0 means no retry middleware is added; consumer is called once.
        var (runner, consumer, writer, adapter) = CreateRunnerWithThrowingConsumer(
            retryCount: 0,
            retryInterval: TimeSpan.Zero);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));

        await writer.WriteAsync(MakeMessage("msg-no-retry"), cts.Token);
        writer.Complete();

        // Act
        await runner.RunAsync(cts.Token);

        // Assert — consumer was invoked exactly once (no retries).
        consumer.CallCount.Should().Be(1);

        // Immediate NACK — no retry delay.
        await adapter.Received(1).SettleAsync(
            SettlementAction.Nack,
            Arg.Is<InboundMessage>(m => m.MessageId == "msg-no-retry"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_WhenDiMiddlewareRegistered_InvokesMiddlewareBeforeConsumer()
    {
        // Arrange — Register a tracking DI middleware that records invocation order.
        List<string> executionLog = [];

        var (runner, writer, adapter, _) = CreateRunnerWithDiMiddleware(
            executionLog,
            diMiddlewareCount: 1);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));

        await writer.WriteAsync(MakeMessage("msg-di-mw"), cts.Token);
        writer.Complete();

        // Act
        await runner.RunAsync(cts.Token);

        // Assert — DI middleware invoked before consumer.
        executionLog.Should().ContainInOrder("di-mw-0", "consumer");
    }

    [Fact]
    public async Task RunAsync_WhenDiMiddlewareRegistered_MiddlewareOrderCorrect()
    {
        // Arrange — Register two DI middleware to verify FIFO ordering.
        // Expected order: DI-0 → DI-1 → Retry → DLQ → consumer
        List<string> executionLog = [];

        var (runner, writer, adapter, _) = CreateRunnerWithDiMiddleware(
            executionLog,
            diMiddlewareCount: 2);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));

        await writer.WriteAsync(MakeMessage("msg-di-order"), cts.Token);
        writer.Complete();

        // Act
        await runner.RunAsync(cts.Token);

        // Assert — DI middleware executed in FIFO order, both before consumer.
        executionLog.Should().ContainInOrder("di-mw-0", "di-mw-1", "consumer");
    }

    private static (
        ReceiveEndpointRunner Runner,
        ChannelWriter<InboundMessage> MessageWriter,
        ITransportAdapter Adapter,
        List<string> ExecutionLog)
        CreateRunnerWithDiMiddleware(List<string> executionLog, int diMiddlewareCount)
    {
        Channel<InboundMessage> channel = Channel.CreateBounded<InboundMessage>(
            new BoundedChannelOptions(64) { SingleWriter = false, SingleReader = true });

        ITransportAdapter adapter = Substitute.For<ITransportAdapter>();
        adapter.TransportName.Returns("test");
        adapter.ConsumeAsync(
                Arg.Any<string>(),
                Arg.Any<FlowControlOptions>(),
                Arg.Any<CancellationToken>())
               .Returns(callInfo => ReadChannelAsync(channel.Reader, callInfo.ArgAt<CancellationToken>(2)));
        adapter.SettleAsync(
                Arg.Any<SettlementAction>(),
                Arg.Any<InboundMessage>(),
                Arg.Any<CancellationToken>())
               .Returns(Task.CompletedTask);

        IMessageDeserializer deserializer = Substitute.For<IMessageDeserializer>();
        deserializer.ContentType.Returns("application/json");

        TrackingRawConsumer consumer = new(executionLog, "consumer");

        // Build DI middleware instances that log their invocation.
        List<IMessageMiddleware> diMiddlewares = [];
        for (int i = 0; i < diMiddlewareCount; i++)
        {
            string label = $"di-mw-{i}";
            TrackingMiddleware mw = new(executionLog, label);
            diMiddlewares.Add(mw);
        }

        IServiceScopeFactory scopeFactory = Substitute.For<IServiceScopeFactory>();
        IServiceScope scope = Substitute.For<IServiceScope>();
        IServiceProvider provider = Substitute.For<IServiceProvider>();

        scopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(provider);
        provider.GetService(typeof(TrackingRawConsumer)).Returns(consumer);

        // GetServices<IMessageMiddleware>() calls GetService(typeof(IEnumerable<IMessageMiddleware>)).
        provider.GetService(typeof(IEnumerable<IMessageMiddleware>))
            .Returns(diMiddlewares);

        FlowController flowController = new(NullLogger<FlowController>.Instance);

        EndpointBinding binding = new()
        {
            EndpointName = EndpointName,
            PrefetchCount = 4,
            Consumers = [],
            RawConsumers = [typeof(TrackingRawConsumer)],
            RetryCount = 0,
            RetryInterval = TimeSpan.Zero,
        };

        ReceiveEndpointRunner runner = new(
            binding,
            adapter,
            deserializer,
            Substitute.For<IPublishEndpoint>(),
            Substitute.For<ISendEndpointProvider>(),
            scopeFactory,
            flowController,
            new NullInstrumentation(),
            NullLogger<ReceiveEndpointRunner>.Instance,
            loggerFactory: NullLoggerFactory.Instance);

        return (runner, channel.Writer, adapter, executionLog);
    }

    /// <summary>
    /// Regression test for Bug 1: RetryMiddleware was silently skipped when loggerFactory was null.
    /// A user setting RetryCount=3 with no ILoggerFactory registered would get zero retries.
    /// Fix: use NullLoggerFactory.Instance fallback instead of guarding with loggerFactory is not null.
    /// </summary>
    [Fact]
    public async Task BuildMiddlewareChain_WhenRetryCountPositiveAndLoggerFactoryNull_IncludesRetryMiddleware()
    {
        // Arrange — RetryCount=2 with loggerFactory=null.
        // Before the fix: consumer would be called exactly once (no retry middleware added).
        // After the fix: consumer must be called 3 times (1 initial + 2 retries).
        var (runner, consumer, writer, adapter) = CreateRunnerWithNullLoggerFactory(retryCount: 2);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));

        await writer.WriteAsync(MakeMessage("msg-null-logger-retry"), cts.Token);
        writer.Complete();

        // Act
        await runner.RunAsync(cts.Token);

        // Assert — RetryMiddleware was active despite null loggerFactory.
        consumer.CallCount.Should().Be(3,
            because: "RetryCount=2 means 1 original attempt + 2 retries regardless of whether loggerFactory is null");

        await adapter.Received(1).SettleAsync(
            SettlementAction.Nack,
            Arg.Is<InboundMessage>(m => m.MessageId == "msg-null-logger-retry"),
            Arg.Any<CancellationToken>());
    }
}
