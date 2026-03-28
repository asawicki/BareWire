namespace BareWire.Outbox.EntityFramework;

/// <summary>
/// PostgreSQL implementation of <see cref="IInboxSqlDialect"/> using <c>ON CONFLICT DO NOTHING</c>.
/// </summary>
internal sealed class PostgresInboxSqlDialect : IInboxSqlDialect
{
    public FormattableString GetUpsertSql(
        Guid messageId,
        string consumerType,
        DateTimeOffset receivedAt,
        DateTimeOffset expiresAt)
        => $"""
            INSERT INTO "InboxMessages" ("MessageId", "ConsumerType", "ReceivedAt", "ExpiresAt")
            VALUES ({messageId}, {consumerType}, {receivedAt}, {expiresAt})
            ON CONFLICT ("MessageId", "ConsumerType") DO NOTHING
            """;
}
