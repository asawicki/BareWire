namespace BareWire.Outbox.EntityFramework;

/// <summary>
/// Provider-specific SQL dialect for inbox upsert operations.
/// </summary>
public interface IInboxSqlDialect
{
    /// <summary>
    /// Returns a parameterized upsert SQL that inserts a new inbox message
    /// or does nothing if the composite key already exists.
    /// The caller uses the rows-affected count to determine whether the insert succeeded (1) or was a duplicate (0).
    /// </summary>
    FormattableString GetUpsertSql(
        Guid messageId,
        string consumerType,
        DateTimeOffset receivedAt,
        DateTimeOffset expiresAt);
}
