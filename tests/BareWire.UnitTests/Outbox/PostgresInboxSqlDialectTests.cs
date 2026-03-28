using System.Globalization;
using AwesomeAssertions;
using BareWire.Outbox.EntityFramework;

namespace BareWire.UnitTests.Outbox;

public sealed class PostgresInboxSqlDialectTests
{
    private readonly PostgresInboxSqlDialect _sut = new();

    [Fact]
    public void GetUpsertSql_ReturnsFormattableStringWithCorrectStructure()
    {
        // Arrange
        Guid messageId = Guid.NewGuid();
        const string consumerType = "order-processor";
        DateTimeOffset receivedAt = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset expiresAt = new(2025, 6, 1, 12, 0, 30, TimeSpan.Zero);

        // Act
        FormattableString sql = _sut.GetUpsertSql(messageId, consumerType, receivedAt, expiresAt);
        string rendered = sql.ToString(CultureInfo.InvariantCulture);

        // Assert — the SQL must target the correct table.
        rendered.Should().Contain("INSERT INTO \"InboxMessages\"",
            "the upsert must target the InboxMessages table");

        // Assert — all four columns must be present.
        rendered.Should().Contain("\"MessageId\"",
            "MessageId column must appear in the INSERT column list");
        rendered.Should().Contain("\"ConsumerType\"",
            "ConsumerType column must appear in the INSERT column list");
        rendered.Should().Contain("\"ReceivedAt\"",
            "ReceivedAt column must appear in the INSERT column list");
        rendered.Should().Contain("\"ExpiresAt\"",
            "ExpiresAt column must appear in the INSERT column list");

        // Assert — PostgreSQL conflict clause must be present for idempotent upsert.
        rendered.Should().Contain("ON CONFLICT",
            "PostgreSQL upsert must use ON CONFLICT clause");
        rendered.Should().Contain("DO NOTHING",
            "duplicate rows must be silently ignored via DO NOTHING");

        // Assert — the composite key columns must appear in the conflict target.
        rendered.Should().Contain("\"MessageId\", \"ConsumerType\"",
            "conflict target must cover the composite key (MessageId, ConsumerType)");
    }
}
