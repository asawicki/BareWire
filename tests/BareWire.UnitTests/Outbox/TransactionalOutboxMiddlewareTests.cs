// NSubstitute's Returns() for ValueTask-returning mocks triggers CA2012 as a known false positive.
// The ValueTask is consumed internally by NSubstitute and never double-consumed.
#pragma warning disable CA2012

using System.Buffers;
using AwesomeAssertions;
using BareWire.Abstractions.Pipeline;
using BareWire.Outbox;
using BareWire.Outbox.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace BareWire.UnitTests.Outbox;

public sealed class TransactionalOutboxMiddlewareTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static MessageContext CreateContext(Guid? messageId = null)
    {
        IServiceProvider serviceProvider = Substitute.For<IServiceProvider>();
        return new MessageContext(
            messageId: messageId ?? Guid.NewGuid(),
            headers: new Dictionary<string, string>(),
            rawBody: ReadOnlySequence<byte>.Empty,
            serviceProvider: serviceProvider,
            endpointName: "test-endpoint");
    }

    /// <summary>
    /// Creates a <see cref="TransactionalOutboxMiddleware"/> with an <see cref="InboxFilter"/>
    /// backed by a mock <see cref="IInboxStore"/> whose <c>TryLockAsync</c> returns
    /// <paramref name="lockAcquired"/>.
    /// </summary>
    private static (TransactionalOutboxMiddleware Middleware, IInboxStore InboxStore)
        CreateMiddleware(bool lockAcquired)
    {
        IInboxStore inboxStore = Substitute.For<IInboxStore>();
        inboxStore
            .TryLockAsync(
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(lockAcquired));

        inboxStore
            .MarkProcessedAsync(
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);

        InboxFilter inboxFilter = new(
            inboxStore,
            OutboxOptions.Default,
            NullLogger<InboxFilter>.Instance);

        // OutboxDbContext requires real options — use an in-memory SQLite database
        // so EF Core can be instantiated without a real server.
        DbContextOptions<OutboxDbContext> dbOptions = new DbContextOptionsBuilder<OutboxDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        OutboxDbContext dbContext = new(dbOptions);

        IOutboxStore outboxStore = Substitute.For<IOutboxStore>();

        TransactionalOutboxMiddleware middleware = new(
            dbContext,
            outboxStore,
            inboxFilter,
            NullLogger<TransactionalOutboxMiddleware>.Instance);

        return (middleware, inboxStore);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_WhenDuplicateDetected_SetsInboxFilteredFlag()
    {
        // Arrange — inbox store signals duplicate (TryLockAsync returns false).
        var (middleware, _) = CreateMiddleware(lockAcquired: false);
        MessageContext context = CreateContext();
        bool nextCalled = false;
        NextMiddleware next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        // Act
        await middleware.InvokeAsync(context, next);

        // Assert — the inbox:filtered flag must be set to true.
        context.HasItems.Should().BeTrue(
            "Items must be allocated when the flag is set");
        context.Items.TryGetValue(WellKnownItemKeys.InboxFiltered, out object? flagValue)
            .Should().BeTrue("'inbox:filtered' key must be present");
        flagValue.Should().Be(true,
            "the flag value must be boolean true");

        _ = nextCalled; // suppress unused-variable warning
    }

    [Fact]
    public async Task InvokeAsync_WhenDuplicateDetected_DoesNotCallNext()
    {
        // Arrange — inbox store signals duplicate (TryLockAsync returns false).
        var (middleware, _) = CreateMiddleware(lockAcquired: false);
        MessageContext context = CreateContext();
        bool nextCalled = false;
        NextMiddleware next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        // Act
        await middleware.InvokeAsync(context, next);

        // Assert — next must NOT have been called when a duplicate is detected.
        nextCalled.Should().BeFalse(
            "the downstream pipeline must not be invoked for duplicate messages");
    }

    [Fact]
    public async Task InvokeAsync_WhenLockAcquired_DoesNotSetInboxFilteredFlag()
    {
        // Arrange — inbox store grants the lock (TryLockAsync returns true).
        var (middleware, _) = CreateMiddleware(lockAcquired: true);
        MessageContext context = CreateContext();

        bool nextCalled = false;
        NextMiddleware next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        // Act
        await middleware.InvokeAsync(context, next);

        // Assert — the inbox:filtered flag must NOT be set when lock is acquired.
        bool hasFlag = context.HasItems
            && context.Items.TryGetValue(WellKnownItemKeys.InboxFiltered, out object? v)
            && v is true;
        hasFlag.Should().BeFalse(
            "the inbox:filtered flag must not be set when the message is processed normally");

        // Sanity check — next was called (lock acquired → proceed with processing).
        nextCalled.Should().BeTrue(
            "the downstream pipeline must be invoked when the inbox lock is acquired");
    }
}
