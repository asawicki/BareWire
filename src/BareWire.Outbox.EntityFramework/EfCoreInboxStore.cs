using Microsoft.EntityFrameworkCore;

namespace BareWire.Outbox.EntityFramework;

internal sealed class EfCoreInboxStore : IInboxStore
{
    private readonly OutboxDbContext _dbContext;

    internal EfCoreInboxStore(OutboxDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async ValueTask<bool> TryLockAsync(
        Guid messageId,
        string consumerType,
        TimeSpan lockTimeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consumerType);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset expiresAt = now + lockTimeout;

        var entry = new InboxMessage
        {
            MessageId = messageId,
            ConsumerType = consumerType,
            ReceivedAt = now,
            ExpiresAt = expiresAt
        };

        _dbContext.InboxMessages.Add(entry);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException)
        {
            // Unique constraint violation — message already in inbox.
            // Detach the failed entity so the DbContext change tracker is clean.
            _dbContext.Entry(entry).State = EntityState.Detached;

            // Check if the existing entry is expired — if so, re-lock it.
            InboxMessage? existing = await _dbContext.Set<InboxMessage>()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    m => m.MessageId == messageId && m.ConsumerType == consumerType,
                    cancellationToken)
                .ConfigureAwait(false);

            if (existing is null || existing.ExpiresAt >= now || existing.ProcessedAt is not null)
            {
                // Not expired, already processed, or somehow missing after the constraint violation — duplicate.
                return false;
            }

            // Expired lock — update ExpiresAt to re-acquire.
            await _dbContext.Set<InboxMessage>()
                .Where(m => m.MessageId == messageId && m.ConsumerType == consumerType)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(m => m.ExpiresAt, expiresAt),
                    cancellationToken)
                .ConfigureAwait(false);

            return true;
        }
    }

    public async ValueTask MarkProcessedAsync(
        Guid messageId,
        string consumerType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consumerType);

        await _dbContext.Set<InboxMessage>()
            .Where(m => m.MessageId == messageId && m.ConsumerType == consumerType)
            .ExecuteUpdateAsync(
                s => s.SetProperty(m => m.ProcessedAt, DateTimeOffset.UtcNow),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask CleanupAsync(
        TimeSpan retention,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset cutoff = DateTimeOffset.UtcNow - retention;

        await _dbContext.Set<InboxMessage>()
            .Where(m => m.ExpiresAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
