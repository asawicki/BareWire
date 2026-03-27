using BareWire.Abstractions.Pipeline;

namespace BareWire.Pipeline;

internal sealed class MiddlewareChain
{
    private readonly IReadOnlyList<IMessageMiddleware> _middlewares;

    internal MiddlewareChain(IReadOnlyList<IMessageMiddleware> middlewares)
    {
        _middlewares = middlewares ?? throw new ArgumentNullException(nameof(middlewares));
    }

    internal Task InvokeAsync(MessageContext context, NextMiddleware terminator)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(terminator);

        if (_middlewares.Count == 0)
            return terminator(context);

        NextMiddleware pipeline = BuildPipeline(terminator);
        return pipeline(context);
    }

    private NextMiddleware BuildPipeline(NextMiddleware terminator)
    {
        // Build the chain from end to start so execution order is FIFO:
        // _middlewares[0] → _middlewares[1] → ... → terminator
        NextMiddleware next = terminator;

        for (int i = _middlewares.Count - 1; i >= 0; i--)
        {
            IMessageMiddleware middleware = _middlewares[i];
            NextMiddleware capturedNext = next;
            next = ctx => middleware.InvokeAsync(ctx, capturedNext);
        }

        return next;
    }
}
