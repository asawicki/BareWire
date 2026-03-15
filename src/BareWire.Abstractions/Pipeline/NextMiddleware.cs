namespace BareWire.Abstractions.Pipeline;

/// <summary>
/// Represents the next step in a middleware pipeline.
/// Invoking this delegate passes control to the subsequent middleware or the terminal handler.
/// </summary>
/// <param name="context">The message context flowing through the pipeline.</param>
/// <returns>A <see cref="Task"/> that completes when the downstream pipeline has finished.</returns>
public delegate Task NextMiddleware(MessageContext context);
