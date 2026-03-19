using BareWire.Abstractions.Outbox;
using BareWire.Outbox;

namespace BareWire.Outbox.EntityFramework;

/// <summary>
/// Mutable configurator that accumulates outbox settings specified by the caller
/// and produces a validated <see cref="OutboxOptions"/> instance.
/// </summary>
internal sealed class OutboxConfigurator : IOutboxConfigurator
{
    private TimeSpan _pollingInterval = OutboxOptions.Default.PollingInterval;
    private int _dispatchBatchSize = OutboxOptions.Default.DispatchBatchSize;
    private TimeSpan _inboxRetention = OutboxOptions.Default.InboxRetention;
    private TimeSpan _outboxRetention = OutboxOptions.Default.OutboxRetention;
    private TimeSpan _inboxLockTimeout = OutboxOptions.Default.InboxLockTimeout;
    private TimeSpan _cleanupInterval = OutboxOptions.Default.CleanupInterval;

    /// <inheritdoc />
    public TimeSpan PollingInterval
    {
        get => _pollingInterval;
        set => _pollingInterval = value;
    }

    /// <inheritdoc />
    public int DispatchBatchSize
    {
        get => _dispatchBatchSize;
        set => _dispatchBatchSize = value;
    }

    /// <inheritdoc />
    public TimeSpan InboxRetention
    {
        get => _inboxRetention;
        set => _inboxRetention = value;
    }

    /// <inheritdoc />
    public TimeSpan OutboxRetention
    {
        get => _outboxRetention;
        set => _outboxRetention = value;
    }

    /// <inheritdoc />
    public TimeSpan InboxLockTimeout
    {
        get => _inboxLockTimeout;
        set => _inboxLockTimeout = value;
    }

    /// <inheritdoc />
    public TimeSpan CleanupInterval
    {
        get => _cleanupInterval;
        set => _cleanupInterval = value;
    }

    /// <summary>
    /// Builds and validates the <see cref="OutboxOptions"/> from the accumulated configuration.
    /// </summary>
    /// <exception cref="BareWire.Abstractions.Exceptions.BareWireConfigurationException">
    /// Thrown when any option value is out of its valid range.
    /// </exception>
    internal OutboxOptions Build()
    {
        var options = new OutboxOptions
        {
            PollingInterval = _pollingInterval,
            DispatchBatchSize = _dispatchBatchSize,
            InboxRetention = _inboxRetention,
            OutboxRetention = _outboxRetention,
            InboxLockTimeout = _inboxLockTimeout,
            CleanupInterval = _cleanupInterval,
        };

        options.Validate();
        return options;
    }
}
