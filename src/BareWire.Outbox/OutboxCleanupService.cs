using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BareWire.Outbox;

internal sealed partial class OutboxCleanupService : IHostedService, IAsyncDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxCleanupService> _logger;

    private CancellationTokenSource? _cts;
    private Task? _cleanupTask;

    public OutboxCleanupService(
        IServiceScopeFactory scopeFactory,
        OutboxOptions options,
        ILogger<OutboxCleanupService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _cleanupTask = RunCleanupLoopAsync(_cts.Token);
        LogCleanupServiceStarted(_logger, _options.CleanupInterval);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        LogCleanupServiceStopping(_logger);

        if (_cts is not null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);
        }

        if (_cleanupTask is not null)
        {
            try
            {
                await _cleanupTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected on graceful shutdown — swallow.
            }
        }

        LogCleanupServiceStopped(_logger);
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);
            _cts.Dispose();
            _cts = null;
        }
    }

    private async Task RunCleanupLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_options.CleanupInterval);

        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            try
            {
                await RunCleanupAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogCleanupError(_logger, ex);
                // Continue — do not crash the hosted service on transient errors.
            }
        }
    }

    private async Task RunCleanupAsync(CancellationToken ct)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        IOutboxStore outboxStore = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
        IInboxStore inboxStore = scope.ServiceProvider.GetRequiredService<IInboxStore>();

        LogCleaningOutbox(_logger, _options.OutboxRetention);
        await outboxStore.CleanupAsync(_options.OutboxRetention, ct).ConfigureAwait(false);

        LogCleaningInbox(_logger, _options.InboxRetention);
        await inboxStore.CleanupAsync(_options.InboxRetention, ct).ConfigureAwait(false);

        LogCleanupCompleted(_logger);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "OutboxCleanupService started. CleanupInterval={CleanupInterval}.")]
    private static partial void LogCleanupServiceStarted(ILogger logger, TimeSpan cleanupInterval);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "OutboxCleanupService stopping.")]
    private static partial void LogCleanupServiceStopping(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "OutboxCleanupService stopped.")]
    private static partial void LogCleanupServiceStopped(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Cleaning outbox records older than {OutboxRetention}.")]
    private static partial void LogCleaningOutbox(ILogger logger, TimeSpan outboxRetention);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Cleaning inbox records older than {InboxRetention}.")]
    private static partial void LogCleaningInbox(ILogger logger, TimeSpan inboxRetention);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Outbox and inbox cleanup completed.")]
    private static partial void LogCleanupCompleted(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Error during outbox/inbox cleanup. Will retry on next tick.")]
    private static partial void LogCleanupError(ILogger logger, Exception ex);
}
