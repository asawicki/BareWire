using Microsoft.EntityFrameworkCore;

namespace BareWire.Outbox.EntityFramework;

internal sealed class EfCoreInboxStore : IInboxStore
{
    private readonly OutboxDbContext _dbContext;
    private readonly IInboxSqlDialect _dialect;

    internal EfCoreInboxStore(OutboxDbContext dbContext, IInboxSqlDialect dialect)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(dialect);
        _dbContext = dbContext;
        _dialect = dialect;
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

        // Upsert via dialect — eliminates DbUpdateException on duplicates.
        int rowsAffected = await _dbContext.Database.ExecuteSqlAsync(
            _dialect.GetUpsertSql(messageId, consumerType, now, expiresAt),
            cancellationToken).ConfigureAwait(false);

        if (rowsAffected > 0)
        {
            return true; // Lock acquired.
        }

        // Existing entry — check if expired and unprocessed → re-lock.
        InboxMessage? existing = await _dbContext.Set<InboxMessage>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.MessageId == messageId && m.ConsumerType == consumerType,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is null || existing.ExpiresAt >= now || existing.ProcessedAt is not null)
        {
            return false; // Not expired, already processed, or missing — duplicate.
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
