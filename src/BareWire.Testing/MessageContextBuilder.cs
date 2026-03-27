using System.Buffers;
using BareWire.Abstractions;
using BareWire.Abstractions.Pipeline;
using NSubstitute;

namespace BareWire.Testing;

/// <summary>
/// A fluent builder for constructing <see cref="ConsumeContext{T}"/> and <see cref="MessageContext"/>
/// instances in unit and integration tests, without needing a running bus or transport.
/// </summary>
/// <remarks>
/// Use <see cref="Create"/> to obtain a new builder instance. Chain <c>With*</c> methods to configure
/// properties, then call <see cref="Build{T}"/> for a typed consume context or
/// <see cref="BuildMessageContext"/> for a middleware-level context.
/// <para>
/// Default values: <c>MessageId = Guid.NewGuid()</c>, all other fields null or empty.
/// <see cref="IPublishEndpoint"/> and <see cref="ISendEndpointProvider"/> default to NSubstitute
/// no-op mocks when not explicitly provided.
/// </para>
/// </remarks>
public sealed class MessageContextBuilder
{
    private Guid _messageId = Guid.NewGuid();
    private Guid? _correlationId;
    private Guid? _conversationId;
    private IReadOnlyDictionary<string, string> _headers = new Dictionary<string, string>();
    private Uri? _sourceAddress;
    private Uri? _destinationAddress;
    private DateTimeOffset? _sentTime;
    private string? _contentType;
    private CancellationToken _cancellationToken;
    private ReadOnlySequence<byte> _rawBody = ReadOnlySequence<byte>.Empty;
    private IPublishEndpoint? _publishEndpoint;
    private ISendEndpointProvider? _sendEndpointProvider;
    private IServiceProvider? _serviceProvider;
    private string _endpointName = string.Empty;

    private MessageContextBuilder() { }

    /// <summary>
    /// Creates a new <see cref="MessageContextBuilder"/> with default values.
    /// </summary>
    public static MessageContextBuilder Create() => new();

    /// <summary>
    /// Sets the message identifier. Defaults to a freshly generated <see cref="Guid.NewGuid()"/>.
    /// </summary>
    public MessageContextBuilder WithMessageId(Guid messageId)
    {
        _messageId = messageId;
        return this;
    }

    /// <summary>
    /// Sets the optional correlation identifier used to correlate related messages.
    /// </summary>
    public MessageContextBuilder WithCorrelationId(Guid correlationId)
    {
        _correlationId = correlationId;
        return this;
    }

    /// <summary>
    /// Sets the optional conversation identifier that groups a chain of related messages.
    /// </summary>
    public MessageContextBuilder WithConversationId(Guid conversationId)
    {
        _conversationId = conversationId;
        return this;
    }

    /// <summary>
    /// Sets the transport-level and application-level headers attached to the message.
    /// </summary>
    public MessageContextBuilder WithHeaders(IReadOnlyDictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        _headers = headers;
        return this;
    }

    /// <summary>
    /// Sets the source address of the endpoint that originated the message.
    /// </summary>
    public MessageContextBuilder WithSourceAddress(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);
        _sourceAddress = address;
        return this;
    }

    /// <summary>
    /// Sets the destination address of the endpoint where the message was delivered.
    /// </summary>
    public MessageContextBuilder WithDestinationAddress(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);
        _destinationAddress = address;
        return this;
    }

    /// <summary>
    /// Sets the UTC time at which the message was sent by the publisher.
    /// </summary>
    public MessageContextBuilder WithSentTime(DateTimeOffset sentTime)
    {
        _sentTime = sentTime;
        return this;
    }

    /// <summary>
    /// Sets the MIME content type of the raw message body.
    /// </summary>
    public MessageContextBuilder WithContentType(string contentType)
    {
        ArgumentNullException.ThrowIfNull(contentType);
        _contentType = contentType;
        return this;
    }

    /// <summary>
    /// Sets the cancellation token used to signal processing abort.
    /// </summary>
    public MessageContextBuilder WithCancellationToken(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        return this;
    }

    /// <summary>
    /// Sets the raw zero-copy body of the message.
    /// </summary>
    public MessageContextBuilder WithRawBody(ReadOnlySequence<byte> rawBody)
    {
        _rawBody = rawBody;
        return this;
    }

    /// <summary>
    /// Overrides the <see cref="IPublishEndpoint"/> used inside the built context.
    /// Defaults to a NSubstitute no-op mock when not set.
    /// </summary>
    public MessageContextBuilder WithPublishEndpoint(IPublishEndpoint publishEndpoint)
    {
        ArgumentNullException.ThrowIfNull(publishEndpoint);
        _publishEndpoint = publishEndpoint;
        return this;
    }

    /// <summary>
    /// Overrides the <see cref="ISendEndpointProvider"/> used inside the built context.
    /// Defaults to a NSubstitute no-op mock when not set.
    /// </summary>
    public MessageContextBuilder WithSendEndpointProvider(ISendEndpointProvider sendEndpointProvider)
    {
        ArgumentNullException.ThrowIfNull(sendEndpointProvider);
        _sendEndpointProvider = sendEndpointProvider;
        return this;
    }

    /// <summary>
    /// Overrides the <see cref="IServiceProvider"/> used inside the built <see cref="MessageContext"/>.
    /// Defaults to a NSubstitute no-op mock when not set.
    /// </summary>
    public MessageContextBuilder WithServiceProvider(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _serviceProvider = serviceProvider;
        return this;
    }

    /// <summary>
    /// Sets the endpoint name that identifies the receive endpoint processing the message.
    /// </summary>
    public MessageContextBuilder WithEndpointName(string endpointName)
    {
        ArgumentNullException.ThrowIfNull(endpointName);
        _endpointName = endpointName;
        return this;
    }

    /// <summary>
    /// Builds a strongly-typed <see cref="ConsumeContext{T}"/> carrying <paramref name="message"/>
    /// as the deserialized payload.
    /// </summary>
    /// <typeparam name="T">The message type. Must be a reference type.</typeparam>
    /// <param name="message">The message payload. Must not be null.</param>
    /// <returns>A fully populated <see cref="ConsumeContext{T}"/> ready for use in tests.</returns>
    public ConsumeContext<T> Build<T>(T message) where T : class
    {
        ArgumentNullException.ThrowIfNull(message);

        IPublishEndpoint publishEndpoint = _publishEndpoint ?? Substitute.For<IPublishEndpoint>();
        ISendEndpointProvider sendEndpointProvider = _sendEndpointProvider ?? Substitute.For<ISendEndpointProvider>();

        return new ConsumeContext<T>(
            message: message,
            messageId: _messageId,
            correlationId: _correlationId,
            conversationId: _conversationId,
            sourceAddress: _sourceAddress,
            destinationAddress: _destinationAddress,
            sentTime: _sentTime,
            headers: _headers,
            contentType: _contentType,
            rawBody: _rawBody,
            publishEndpoint: publishEndpoint,
            sendEndpointProvider: sendEndpointProvider,
            cancellationToken: _cancellationToken);
    }

    /// <summary>
    /// Builds a <see cref="MessageContext"/> suitable for testing middleware components that
    /// operate at the pipeline level, before typed deserialization.
    /// </summary>
    /// <returns>A fully populated <see cref="MessageContext"/> ready for use in middleware tests.</returns>
    public MessageContext BuildMessageContext()
    {
        IServiceProvider serviceProvider = _serviceProvider ?? Substitute.For<IServiceProvider>();

        return new MessageContext(
            messageId: _messageId,
            headers: _headers,
            rawBody: _rawBody,
            serviceProvider: serviceProvider,
            endpointName: _endpointName,
            cancellationToken: _cancellationToken);
    }
}
