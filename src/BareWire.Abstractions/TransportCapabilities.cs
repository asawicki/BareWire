namespace BareWire.Abstractions;

/// <summary>
/// Describes the optional features supported by a specific transport adapter.
/// Multiple capabilities can be combined using bitwise OR.
/// </summary>
[Flags]
public enum TransportCapabilities
{
    /// <summary>
    /// The transport has no optional capabilities beyond basic send and receive.
    /// </summary>
    None = 0,

    /// <summary>
    /// The transport supports publisher confirms — acknowledgement that a broker has durably received a message.
    /// </summary>
    PublisherConfirms = 1,

    /// <summary>
    /// The transport provides exactly-once delivery semantics, preventing duplicate processing.
    /// </summary>
    ExactlyOnce = 2,

    /// <summary>
    /// The transport supports distributed transactions spanning multiple operations atomically.
    /// </summary>
    Transactions = 4,

    /// <summary>
    /// The transport performs native deduplication based on a message identifier, preventing duplicate delivery.
    /// </summary>
    NativeDeduplication = 8,

    /// <summary>
    /// The transport supports sessions (stateful, ordered message groups tied to a session identifier).
    /// </summary>
    Sessions = 16,

    /// <summary>
    /// The transport supports ordering keys to guarantee message order within a partition or shard.
    /// </summary>
    OrderingKeys = 32,

    /// <summary>
    /// The transport provides native message scheduling — delivery at a specified future point in time.
    /// </summary>
    NativeScheduling = 64,

    /// <summary>
    /// The transport supports batch receive — retrieving multiple messages in a single network round-trip.
    /// </summary>
    BatchReceive = 128,

    /// <summary>
    /// The transport provides native dead-letter queue (DLQ) support — automatic routing of failed messages.
    /// </summary>
    DlqNative = 256,

    /// <summary>
    /// The transport participates in BareWire credit-based flow control to prevent consumer overload.
    /// </summary>
    FlowControl = 512,
}
