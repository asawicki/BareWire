// NSubstitute's Returns() for ValueTask-returning mocks triggers CA2012 as a false positive.
// The ValueTask is consumed internally by NSubstitute and never double-consumed.
#pragma warning disable CA2012

using AwesomeAssertions;
using BareWire.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace BareWire.UnitTests.Outbox;

public sealed class OutboxCleanupServiceTests : IAsyncDisposable
{
    private readonly IOutboxStore _outboxStore;
    private readonly IInboxStore _inboxStore;
    private readonly ILogger<OutboxCleanupService> _logger;
    private readonly OutboxOptions _options;
    private readonly OutboxCleanupService _sut;

    public OutboxCleanupServiceTests()
    {
        _outboxStore = Substitute.For<IOutboxStore>();
        _inboxStore = Substitute.For<IInboxStore>();
        _logger = Substitute.For<ILogger<OutboxCleanupService>>();

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IOutboxStore)).Returns(_outboxStore);
        serviceProvider.GetService(typeof(IInboxStore)).Returns(_inboxStore);
        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        // Use a short cleanup interval so the loop fires quickly in tests.
        _options = new OutboxOptions
        {
            CleanupInterval = TimeSpan.FromMilliseconds(10),
            OutboxRetention = TimeSpan.FromDays(7),
            InboxRetention = TimeSpan.FromDays(8),   // must be > InboxLockTimeout (30s)
        };

        _sut = new OutboxCleanupService(scopeFactory, _options, _logger);
    }

    public async ValueTask DisposeAsync()
    {
        await _sut.DisposeAsync();
    }

    [Fact]
    public async Task ExecuteAsync_CleansExpiredOutboxRecords()
    {
        // Arrange
        _outboxStore
            .CleanupAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.CompletedTask);

        _inboxStore
            .CleanupAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.CompletedTask);

        // Act — start, let at least one tick fire, then stop
        await _sut.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(80));
        await _sut.StopAsync(CancellationToken.None);

        // Assert — outbox cleanup was called with the configured retention
        await _outboxStore.Received().CleanupAsync(
            _options.OutboxRetention,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_CleansExpiredInboxRecords()
    {
        // Arrange
        _outboxStore
            .CleanupAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.CompletedTask);

        _inboxStore
            .CleanupAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.CompletedTask);

        // Act — start, let at least one tick fire, then stop
        await _sut.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(80));
        await _sut.StopAsync(CancellationToken.None);

        // Assert — inbox cleanup was called with the configured retention
        await _inboxStore.Received().CleanupAsync(
            _options.InboxRetention,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_RecentRecords_NotCleaned()
    {
        // The store decides which records to delete based on the retention value.
        // The cleanup service must pass the correct retention so the store can
        // distinguish recent records from expired ones.

        // Arrange
        TimeSpan? capturedOutboxRetention = null;
        TimeSpan? capturedInboxRetention = null;

        _outboxStore
            .CleanupAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                capturedOutboxRetention = call.Arg<TimeSpan>();
                return ValueTask.CompletedTask;
            });

        _inboxStore
            .CleanupAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                capturedInboxRetention = call.Arg<TimeSpan>();
                return ValueTask.CompletedTask;
            });

        // Act
        await _sut.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(80));
        await _sut.StopAsync(CancellationToken.None);

        // Assert — retention values are passed through exactly as configured,
        // allowing the store to filter out only records older than retention.
        capturedOutboxRetention.Should().Be(_options.OutboxRetention);
        capturedInboxRetention.Should().Be(_options.InboxRetention);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyStores_NoError()
    {
        // Arrange — stores complete normally when empty (no exception thrown)
        _outboxStore
            .CleanupAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.CompletedTask);

        _inboxStore
            .CleanupAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.CompletedTask);

        // Act
        await _sut.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(80));

        Func<Task> act = () => _sut.StopAsync(CancellationToken.None);

        // Assert — no exceptions thrown even when stores are empty
        await act.Should().NotThrowAsync();
    }
}
