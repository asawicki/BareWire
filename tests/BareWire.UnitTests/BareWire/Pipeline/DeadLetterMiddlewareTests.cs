using System.Buffers;
using AwesomeAssertions;
using BareWire.Abstractions.Pipeline;
using BareWire.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace BareWire.UnitTests.Core.Pipeline;

public sealed class DeadLetterMiddlewareTests
{
    private static MessageContext CreateContext(CancellationToken cancellationToken = default)
    {
        IServiceProvider serviceProvider = Substitute.For<IServiceProvider>();
        return new MessageContext(
            messageId: Guid.NewGuid(),
            headers: new Dictionary<string, string>(),
            rawBody: ReadOnlySequence<byte>.Empty,
            serviceProvider: serviceProvider,
            cancellationToken: cancellationToken);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_SuccessfulProcessing_NoDeadLetter()
    {
        // Arrange
        bool deadLetterCalled = false;
        var sut = new DeadLetterMiddleware(
            (_, _) =>
            {
                deadLetterCalled = true;
                return Task.CompletedTask;
            },
            NullLogger<DeadLetterMiddleware>.Instance);
        var context = CreateContext();

        // Act
        await sut.InvokeAsync(context, _ => Task.CompletedTask);

        // Assert
        deadLetterCalled.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_ExceptionAfterRetry_RoutesToDeadLetterThenRethrows()
    {
        // Arrange
        bool deadLetterCalled = false;
        var sut = new DeadLetterMiddleware(
            (_, _) =>
            {
                deadLetterCalled = true;
                return Task.CompletedTask;
            },
            NullLogger<DeadLetterMiddleware>.Instance);
        var context = CreateContext();

        // Act — simulate exception after retry exhaustion
        Func<Task> act = () => sut.InvokeAsync(context, _ =>
            throw new InvalidOperationException("retries exhausted"));

        // Assert — callback fires, then exception propagates so ReceiveEndpointRunner can NACK
        await act.Should().ThrowAsync<InvalidOperationException>();
        deadLetterCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_OnDeadLetterCallback_InvokedWithContextAndException()
    {
        // Arrange
        MessageContext? capturedContext = null;
        Exception? capturedException = null;
        var sut = new DeadLetterMiddleware(
            (ctx, ex) =>
            {
                capturedContext = ctx;
                capturedException = ex;
                return Task.CompletedTask;
            },
            NullLogger<DeadLetterMiddleware>.Instance);
        var context = CreateContext();
        var expectedException = new InvalidOperationException("test error");

        // Act — DeadLetterMiddleware re-throws after invoking the callback
        Func<Task> act = () => sut.InvokeAsync(context, _ => throw expectedException);
        await act.Should().ThrowAsync<InvalidOperationException>();

        // Assert
        capturedContext.Should().BeSameAs(context);
        capturedException.Should().BeSameAs(expectedException);
    }

    [Fact]
    public async Task InvokeAsync_ExceptionThrown_RethrowsAfterCallback()
    {
        // Arrange — DeadLetterMiddleware re-throws so ReceiveEndpointRunner can NACK.
        // The broker then routes to the DLX via x-dead-letter-exchange.
        var thrownException = new InvalidOperationException("failure");
        var sut = new DeadLetterMiddleware(
            (_, _) => Task.CompletedTask,
            NullLogger<DeadLetterMiddleware>.Instance);
        var context = CreateContext();

        // Act
        Func<Task> act = () => sut.InvokeAsync(context, _ => throw thrownException);

        // Assert — exception propagates so the transport layer can NACK
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("failure");
    }

    // ── MUT-773: Constructor null guard — onDeadLetter ────────────────────────

    [Fact]
    public void Constructor_WhenOnDeadLetterIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        Func<MessageContext, Exception, Task>? nullOnDeadLetter = null;

        // Act
        Action act = () => _ = new DeadLetterMiddleware(
            nullOnDeadLetter!,
            NullLogger<DeadLetterMiddleware>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("onDeadLetter");
    }

    // ── MUT-774: Constructor null guard — logger ──────────────────────────────

    [Fact]
    public void Constructor_WhenLoggerIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        ILogger<DeadLetterMiddleware>? nullLogger = null;

        // Act
        Action act = () => _ = new DeadLetterMiddleware(
            (_, _) => Task.CompletedTask,
            nullLogger!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    // ── MUT-776: InvokeAsync null guard — context ─────────────────────────────

    [Fact]
    public async Task InvokeAsync_WhenContextIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        var sut = new DeadLetterMiddleware(
            (_, _) => Task.CompletedTask,
            NullLogger<DeadLetterMiddleware>.Instance);

        // Act
        Func<Task> act = () => sut.InvokeAsync(null!, _ => Task.CompletedTask);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("context");
    }

    // ── MUT-777: InvokeAsync null guard — nextMiddleware ─────────────────────

    [Fact]
    public async Task InvokeAsync_WhenNextMiddlewareIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        var sut = new DeadLetterMiddleware(
            (_, _) => Task.CompletedTask,
            NullLogger<DeadLetterMiddleware>.Instance);
        var context = CreateContext();

        // Act
        Func<Task> act = () => sut.InvokeAsync(context, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("nextMiddleware");
    }

    // ── MUT-782: OperationCanceledException must propagate, not be swallowed ──

    [Fact]
    public async Task InvokeAsync_WhenCancellationOccurs_ShouldPropagateOperationCanceledException()
    {
        // Arrange
        bool deadLetterCalled = false;
        var sut = new DeadLetterMiddleware(
            (_, _) =>
            {
                deadLetterCalled = true;
                return Task.CompletedTask;
            },
            NullLogger<DeadLetterMiddleware>.Instance);
        var context = CreateContext();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        NextMiddleware throwsCancellation = _ => throw new OperationCanceledException(cts.Token);

        // Act
        Func<Task> act = () => sut.InvokeAsync(context, throwsCancellation);

        // Assert — cancellation propagates and the dead-letter callback is NOT invoked
        await act.Should().ThrowAsync<OperationCanceledException>();
        deadLetterCalled.Should().BeFalse("cancellation must not trigger dead-lettering");
    }

    // ── MUT-784: Error log emitted when exception is dead-lettered ────────────

    [Fact]
    public async Task InvokeAsync_WhenExceptionThrown_ShouldLogErrorWithMessageIdAndExceptionType()
    {
        // Arrange
        // DeadLetterMiddleware is internal, so NSubstitute cannot create ILogger<DeadLetterMiddleware>
        // via Castle DynamicProxy (the generic type argument is inaccessible to the proxy assembly).
        // Use a hand-rolled spy that delegates to a substituted non-generic ILogger.
        ILogger innerLogger = Substitute.For<ILogger>();
        innerLogger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        var logger = new SpyLogger<DeadLetterMiddleware>(innerLogger);

        var context = CreateContext();
        var thrownException = new InvalidOperationException("boom");

        var sut = new DeadLetterMiddleware(
            (_, _) => Task.CompletedTask,
            logger);

        // Act
        Func<Task> act = () => sut.InvokeAsync(context, _ => throw thrownException);
        await act.Should().ThrowAsync<InvalidOperationException>();

        // Assert — RoutingToDeadLetter [LoggerMessage] must emit exactly one Error-level log entry
        IEnumerable<NSubstitute.Core.ICall> errorCalls = innerLogger.ReceivedCalls()
            .Where(c =>
                c.GetMethodInfo().Name == nameof(ILogger.Log)
                && c.GetArguments()[0] is LogLevel level
                && level == LogLevel.Error);

        errorCalls.Should().ContainSingle(
            "exactly one Error log must be emitted when routing to dead-letter");
    }

    /// <summary>
    /// Thin typed wrapper around a substituted non-generic <see cref="ILogger"/> that lets
    /// tests spy on log calls when the generic type argument is an internal class (which
    /// Castle.DynamicProxy cannot proxy directly because the assembly is strong-named).
    /// </summary>
    private sealed class SpyLogger<T>(ILogger inner) : ILogger<T>
    {
        void ILogger.Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => inner.Log(logLevel, eventId, state, exception, formatter);

        bool ILogger.IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);

        IDisposable? ILogger.BeginScope<TState>(TState state) => inner.BeginScope(state);
    }
}
