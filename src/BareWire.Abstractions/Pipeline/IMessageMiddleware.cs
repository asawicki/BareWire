namespace BareWire.Abstractions.Pipeline;

/// <summary>
/// Defines a middleware component in the BareWire message processing pipeline.
/// Middleware is invoked in registration order for every inbound message before it reaches a consumer.
/// Implementations are resolved from the DI container and may be singletons or scoped.
/// </summary>
public interface IMessageMiddleware
{
    /// <summary>
    /// Processes the message context and invokes the next middleware in the pipeline.
    /// Implementations must call <c>await nextMiddleware(context)</c> to continue the pipeline,
    /// unless they intentionally short-circuit (e.g. for authorization or deduplication).
    /// </summary>
    /// <param name="context">The message context for the current pipeline invocation.</param>
    /// <param name="nextMiddleware">
    /// The delegate representing the next middleware or terminal consumer handler.
    /// Must be awaited to continue processing.
    /// </param>
    /// <returns>A <see cref="Task"/> that completes when this middleware and all downstream
    /// middleware have finished.</returns>
    Task InvokeAsync(MessageContext context, NextMiddleware nextMiddleware);
}
