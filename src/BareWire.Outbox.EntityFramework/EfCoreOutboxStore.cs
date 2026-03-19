using System.Buffers;
using System.Text.Json;
using BareWire.Abstractions.Transport;
using Microsoft.EntityFrameworkCore;

namespace BareWire.Outbox.EntityFramework;

internal sealed class EfCoreOutboxStore : IOutboxStore
{
    private readonly OutboxDbContext _dbContext;

    internal EfCoreOutboxStore(OutboxDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public ValueTask SaveMessagesAsync(
        IReadOnlyList<OutboundMessage> messages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        if (messages.Count == 0)
        {
            return ValueTask.CompletedTask;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach (OutboundMessage message in messages)
        {
            // Copy ReadOnlyMemory<byte> to byte[] for DB persistence — not a hot path per ADR-003.
            byte[] payload = message.Body.ToArray();

            // Serialize headers to JSON string.
            string? headersJson = message.Headers.Count > 0
                ? JsonSerializer.Serialize(message.Headers)
                : null;

            var entity = new OutboxMessage
            {
                MessageId = Guid.NewGuid(),
                DestinationAddress = message.RoutingKey,
                ContentType = message.ContentType,
                Payload = payload,
                Headers = headersJson,
                CreatedAt = now
            };

            _dbContext.OutboxMessages.Add(entity);
        }

        // Do NOT call SaveChanges — the ambient transaction (TransactionalOutboxMiddleware) handles it.
        return ValueTask.CompletedTask;
    }

    public async ValueTask<IReadOnlyList<OutboxEntry>> GetPendingAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        // Materialize in memory first — ArrayPool cannot be used inside LINQ-to-SQL.
        List<OutboxMessage> rows = await _dbContext.Set<OutboxMessage>()
            .AsNoTracking()
            .Where(m => m.DeliveredAt == null)
            .OrderBy(m => m.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var entries = new List<OutboxEntry>(rows.Count);

        foreach (OutboxMessage row in rows)
        {
            // Rent a pooled buffer and copy payload — caller is responsible for returning to pool.
            byte[] pooledBody = ArrayPool<byte>.Shared.Rent(row.Payload.Length);
            row.Payload.CopyTo(pooledBody, 0);

            IReadOnlyDictionary<string, string> headers = row.Headers is not null
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(row.Headers)
                    ?? (IReadOnlyDictionary<string, string>)new Dictionary<string, string>()
                : new Dictionary<string, string>();

            entries.Add(new OutboxEntry
            {
                Id = row.Id,
                RoutingKey = row.DestinationAddress,
                Headers = headers,
                PooledBody = pooledBody,
                BodyLength = row.Payload.Length,
                ContentType = row.ContentType,
                CreatedAt = row.CreatedAt,
                Status = OutboxEntryStatus.Pending
            });
        }

        return entries;
    }

    public async ValueTask MarkDeliveredAsync(
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            return;
        }

        await _dbContext.Set<OutboxMessage>()
            .Where(m => ids.Contains(m.Id))
            .ExecuteUpdateAsync(
                s => s.SetProperty(m => m.DeliveredAt, DateTimeOffset.UtcNow),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask CleanupAsync(
        TimeSpan retention,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset cutoff = DateTimeOffset.UtcNow - retention;

        await _dbContext.Set<OutboxMessage>()
            .Where(m => m.DeliveredAt != null && m.DeliveredAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
