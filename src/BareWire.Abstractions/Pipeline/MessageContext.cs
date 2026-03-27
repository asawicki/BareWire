using System.Buffers;

namespace BareWire.Abstractions.Pipeline;

/// <summary>
/// Carries all data and services available to middleware components during message processing.
/// Instances are created by the pipeline infrastructure and flow through each middleware in sequence.
/// </summary>
public sealed class MessageContext
{
    /// <summary>
    /// Gets the unique identifier of the message being processed.
    /// </summary>
    public Guid MessageId { get; }

    /// <summary>
    /// Gets the transport-level and application-level headers attached to the message.
    /// Never null — an empty dictionary is returned when no headers are present.
    /// </summary>
    public IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>
    /// Gets the raw zero-copy body of the message as received from the transport.
    /// The sequence is valid only for the duration of the pipeline invocation; it must not be retained
    /// beyond the lifetime of this context.
    /// </summary>
    public ReadOnlySequence<byte> RawBody { get; }

    /// <summary>
    /// Gets the cancellation token that signals that message processing should be aborted.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Gets the scoped <see cref="IServiceProvider"/> for the current message processing scope.
    /// Use this to resolve per-message services registered with the DI container.
    /// </summary>
    public IServiceProvider ServiceProvider { get; }

    /// <summary>The name of the receive endpoint processing this message.</summary>
    public string EndpointName { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="MessageContext"/>.
    /// </summary>
    /// <param name="messageId">The unique identifier of the message.</param>
    /// <param name="headers">The message headers. Must not be null.</param>
    /// <param name="rawBody">The raw zero-copy body of the message.</param>
    /// <param name="serviceProvider">The scoped service provider for the current message. Must not be null.</param>
    /// <param name="cancellationToken">The cancellation token for this processing scope.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="headers"/> or <paramref name="serviceProvider"/> is null.
    /// </exception>
    public MessageContext(
        Guid messageId,
        IReadOnlyDictionary<string, string> headers,
        ReadOnlySequence<byte> rawBody,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        MessageId = messageId;
        Headers = headers ?? throw new ArgumentNullException(nameof(headers));
        RawBody = rawBody;
        ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        CancellationToken = cancellationToken;
        EndpointName = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="MessageContext"/> with an explicit endpoint name.
    /// </summary>
    /// <param name="messageId">The unique identifier of the message.</param>
    /// <param name="headers">The message headers. Must not be null.</param>
    /// <param name="rawBody">The raw zero-copy body of the message.</param>
    /// <param name="serviceProvider">The scoped service provider for the current message. Must not be null.</param>
    /// <param name="endpointName">The name of the receive endpoint processing this message. Must not be null.</param>
    /// <param name="cancellationToken">The cancellation token for this processing scope.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="headers"/>, <paramref name="serviceProvider"/>,
    /// or <paramref name="endpointName"/> is null.
    /// </exception>
    public MessageContext(
        Guid messageId,
        IReadOnlyDictionary<string, string> headers,
        ReadOnlySequence<byte> rawBody,
        IServiceProvider serviceProvider,
        string endpointName,
        CancellationToken cancellationToken = default)
    {
        MessageId = messageId;
        Headers = headers ?? throw new ArgumentNullException(nameof(headers));
        RawBody = rawBody;
        ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        EndpointName = endpointName ?? throw new ArgumentNullException(nameof(endpointName));
        CancellationToken = cancellationToken;
    }
}
