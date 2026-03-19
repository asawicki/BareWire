using System.Text.Json;

namespace BareWire.Serialization.Json;

/// <summary>
/// Internal DTO representing the BareWire envelope format (ADR-001 opt-in).
/// The <see cref="Body"/> is stored as a <see cref="JsonElement"/> to allow lazy, zero-copy
/// extraction of the message payload without a second deserialization pass over the envelope wrapper.
/// </summary>
internal sealed record BareWireEnvelope(
    Guid MessageId,
    Guid? CorrelationId,
    Guid? ConversationId,
    string[]? MessageType,
    DateTimeOffset SentTime,
    IReadOnlyDictionary<string, string>? Headers,
    JsonElement Body);
