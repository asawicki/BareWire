using System.Buffers;
using BareWire.Abstractions.Serialization;

namespace BareWire.Abstractions.Saga;

/// <summary>
/// Dispatches a raw inbound message to a saga state machine.
/// Implementations are provided by <c>BareWire.Saga</c> and registered in DI by
/// <c>AddBareWireSagaStateMachine&lt;TStateMachine, TSaga&gt;()</c>. The core bus resolves all
/// registered dispatchers for each receive endpoint that declares saga types.
/// </summary>
public interface ISagaMessageDispatcher
{
    /// <summary>
    /// Gets the CLR type of the state machine this dispatcher serves.
    /// Used by the bus to match dispatchers to endpoint bindings.
    /// </summary>
    Type StateMachineType { get; }

    /// <summary>
    /// Attempts to deserialize the message body and dispatch it to the saga state machine.
    /// Returns <see langword="true"/> when a matching event type was found and processed.
    /// Returns <see langword="false"/> when the message body does not match any event type
    /// handled by this saga, allowing the bus to try the next consumer or dispatcher.
    /// </summary>
    /// <param name="body">The raw inbound message body from the transport.</param>
    /// <param name="headers">The transport-level and application-level message headers.</param>
    /// <param name="messageId">The transport message identifier, used for correlation tracing.</param>
    /// <param name="publishEndpoint">The publish endpoint available during saga activity execution.</param>
    /// <param name="sendEndpointProvider">The send endpoint provider available during saga activity execution.</param>
    /// <param name="deserializer">The message deserializer to use when decoding the message body.</param>
    /// <param name="cancellationToken">A token to cancel the dispatch operation.</param>
    /// <returns>
    /// <see langword="true"/> if the message was successfully dispatched to the saga;
    /// <see langword="false"/> if no event type registered on this saga matched the message.
    /// </returns>
    Task<bool> TryDispatchAsync(
        ReadOnlySequence<byte> body,
        IReadOnlyDictionary<string, string> headers,
        string messageId,
        IPublishEndpoint publishEndpoint,
        ISendEndpointProvider sendEndpointProvider,
        IMessageDeserializer deserializer,
        CancellationToken cancellationToken = default);
}
