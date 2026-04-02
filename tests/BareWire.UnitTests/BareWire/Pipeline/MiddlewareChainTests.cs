using System.Buffers;
using AwesomeAssertions;
using BareWire.Abstractions.Pipeline;
using BareWire.Pipeline;
using NSubstitute;

namespace BareWire.UnitTests.Core.Pipeline;

public sealed class MiddlewareChainTests
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

    [Fact]
    public async Task InvokeAsync_EmptyChain_CallsTerminator()
    {
        var chain = new MiddlewareChain([]);
        bool terminatorCalled = false;

        await chain.InvokeAsync(CreateContext(), ctx =>
        {
            terminatorCalled = true;
            return Task.CompletedTask;
        });

        terminatorCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_SingleMiddleware_CallsMiddlewareThenTerminator()
    {
        var executionOrder = new List<string>();

        IMessageMiddleware middleware = Substitute.For<IMessageMiddleware>();
        middleware.InvokeAsync(Arg.Any<MessageContext>(), Arg.Any<NextMiddleware>())
            .Returns(async callInfo =>
            {
                executionOrder.Add("middleware");
                await callInfo.ArgAt<NextMiddleware>(1)(callInfo.ArgAt<MessageContext>(0));
            });

        var chain = new MiddlewareChain([middleware]);

        await chain.InvokeAsync(CreateContext(), ctx =>
        {
            executionOrder.Add("terminator");
            return Task.CompletedTask;
        });

        executionOrder.Should().Equal("middleware", "terminator");
    }

    [Fact]
    public async Task InvokeAsync_MultipleMiddlewares_ExecutesInFIFOOrder()
    {
        var executionOrder = new List<string>();

        IMessageMiddleware Middleware(string name)
        {
            IMessageMiddleware m = Substitute.For<IMessageMiddleware>();
            m.InvokeAsync(Arg.Any<MessageContext>(), Arg.Any<NextMiddleware>())
                .Returns(async callInfo =>
                {
                    executionOrder.Add(name);
                    await callInfo.ArgAt<NextMiddleware>(1)(callInfo.ArgAt<MessageContext>(0));
                });
            return m;
        }

        var chain = new MiddlewareChain([Middleware("first"), Middleware("second"), Middleware("third")]);

        await chain.InvokeAsync(CreateContext(), ctx =>
        {
            executionOrder.Add("terminator");
            return Task.CompletedTask;
        });

        executionOrder.Should().Equal("first", "second", "third", "terminator");
    }

    [Fact]
    public async Task InvokeAsync_MiddlewareThrows_PropagatesException()
    {
        IMessageMiddleware middleware = Substitute.For<IMessageMiddleware>();
        middleware.InvokeAsync(Arg.Any<MessageContext>(), Arg.Any<NextMiddleware>())
            .Returns<Task>(_ => throw new InvalidOperationException("middleware error"));

        var chain = new MiddlewareChain([middleware]);

        Func<Task> act = () => chain.InvokeAsync(CreateContext(), _ => Task.CompletedTask);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("middleware error");
    }

    [Fact]
    public async Task InvokeAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var capturedToken = CancellationToken.None;

        IMessageMiddleware middleware = Substitute.For<IMessageMiddleware>();
        middleware.InvokeAsync(Arg.Any<MessageContext>(), Arg.Any<NextMiddleware>())
            .Returns(callInfo =>
            {
                capturedToken = callInfo.ArgAt<MessageContext>(0).CancellationToken;
                return Task.CompletedTask;
            });

        var chain = new MiddlewareChain([middleware]);
        MessageContext context = CreateContext(cts.Token);

        await chain.InvokeAsync(context, _ => Task.CompletedTask);

        capturedToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task InvokeAsync_MiddlewareDoesNotCallNext_TerminatorNotCalled()
    {
        bool terminatorCalled = false;

        // Middleware that short-circuits — never calls next.
        IMessageMiddleware middleware = Substitute.For<IMessageMiddleware>();
        middleware.InvokeAsync(Arg.Any<MessageContext>(), Arg.Any<NextMiddleware>())
            .Returns(Task.CompletedTask);

        var chain = new MiddlewareChain([middleware]);

        await chain.InvokeAsync(CreateContext(), ctx =>
        {
            terminatorCalled = true;
            return Task.CompletedTask;
        });

        terminatorCalled.Should().BeFalse();
    }

    [Fact]
    public void InvokeAsync_NullMiddlewares_ThrowsArgumentNullException()
    {
        // The constructor validates the middlewares list — passing null must throw immediately.
        Action act = () => _ = new MiddlewareChain(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void InvokeAsync_WhenContextIsNull_ThrowsArgumentNullException()
    {
        var chain = new MiddlewareChain([]);
        NextMiddleware terminator = _ => Task.CompletedTask;

        Action act = () => chain.InvokeAsync(null!, terminator);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    public void InvokeAsync_WhenTerminatorIsNull_ThrowsArgumentNullException()
    {
        var chain = new MiddlewareChain([]);

        Action act = () => chain.InvokeAsync(CreateContext(), null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("terminator");
    }

    [Fact]
    public async Task InvokeAsync_CancellationDuringMiddleware_PropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();

        // Middleware that cancels the token and then throws OperationCanceledException.
        IMessageMiddleware middleware = Substitute.For<IMessageMiddleware>();
        middleware.InvokeAsync(Arg.Any<MessageContext>(), Arg.Any<NextMiddleware>())
            .Returns(callInfo =>
            {
                cts.Cancel();
                callInfo.ArgAt<MessageContext>(0).CancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            });

        var chain = new MiddlewareChain([middleware]);
        MessageContext context = CreateContext(cts.Token);

        Func<Task> act = () => chain.InvokeAsync(context, _ => Task.CompletedTask);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
