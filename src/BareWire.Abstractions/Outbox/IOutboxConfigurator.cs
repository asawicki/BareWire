namespace BareWire.Abstractions.Outbox;

/// <summary>
/// Configures the outbox/inbox pattern for reliable message delivery.
/// All messages published inside a consumer handler are buffered and dispatched
/// only after the handler completes successfully, providing at-least-once delivery guarantees.
/// </summary>
public interface IOutboxConfigurator
{
    /// <summary>
    /// Gets or sets how frequently the outbox dispatcher polls for pending messages.
    /// Defaults to <c>1 second</c>.
    /// </summary>
    TimeSpan PollingInterval { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of outbox messages dispatched in a single polling cycle.
    /// Defaults to <c>100</c>.
    /// </summary>
    int DispatchBatchSize { get; set; }

    /// <summary>
    /// Gets or sets how long processed inbox entries are retained before cleanup removes them.
    /// Must be greater than <see cref="InboxLockTimeout"/> to prevent a lock from expiring
    /// before its entry is cleaned up.
    /// Defaults to <c>7 days</c>.
    /// </summary>
    TimeSpan InboxRetention { get; set; }

    /// <summary>
    /// Gets or sets how long delivered outbox entries are retained before cleanup removes them.
    /// Defaults to <c>7 days</c>.
    /// </summary>
    TimeSpan OutboxRetention { get; set; }

    /// <summary>
    /// Gets or sets how long an inbox lock is held before a message is considered safe to re-process.
    /// If a consumer crashes mid-processing, the lock expires and the message can be retried.
    /// Defaults to <c>30 seconds</c>.
    /// </summary>
    TimeSpan InboxLockTimeout { get; set; }

    /// <summary>
    /// Gets or sets how frequently the cleanup service removes expired outbox and inbox entries.
    /// Defaults to <c>1 hour</c>.
    /// </summary>
    TimeSpan CleanupInterval { get; set; }

    /// <summary>
    /// When <see langword="true"/>, Outbox/Inbox tables are created automatically at host startup.
    /// Default: <see langword="false"/>.
    /// </summary>
    bool AutoCreateSchema { get; set; }
}
