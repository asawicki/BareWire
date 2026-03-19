using Microsoft.Extensions.Logging;

namespace BareWire.Outbox;

internal sealed partial class InboxFilter
{
    private readonly IInboxStore _store;
    private readonly OutboxOptions _options;
    private readonly ILogger<InboxFilter> _logger;

    internal InboxFilter(IInboxStore store, OutboxOptions options, ILogger<InboxFilter> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    internal async ValueTask<bool> TryLockAsync(
        Guid messageId,
        string consumerType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consumerType);

        bool acquired = await _store
            .TryLockAsync(messageId, consumerType, _options.InboxLockTimeout, cancellationToken)
            .ConfigureAwait(false);

        if (!acquired)
        {
            InboxFilterLogMessages.DuplicateMessageSkipped(_logger, messageId);
        }

        return acquired;
    }
}

internal static partial class InboxFilterLogMessages
{
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Duplicate message {MessageId} skipped — lock already held")]
    internal static partial void DuplicateMessageSkipped(
        ILogger logger,
        Guid messageId);
}
