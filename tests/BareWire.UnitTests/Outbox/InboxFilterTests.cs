#pragma warning disable CA2012 // NSubstitute .Returns() on ValueTask<T> is a known false positive

using AwesomeAssertions;
using BareWire.Outbox;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace BareWire.UnitTests.Outbox;

public sealed class InboxFilterTests
{
    private readonly IInboxStore _store;
    private readonly ILogger<InboxFilter> _logger;
    private readonly OutboxOptions _options;
    private readonly InboxFilter _sut;

    public InboxFilterTests()
    {
        _store = Substitute.For<IInboxStore>();
        _logger = Substitute.For<ILogger<InboxFilter>>();
        _options = OutboxOptions.Default;
        _sut = new InboxFilter(_store, _options, _logger);
    }

    [Fact]
    public async Task TryLockAsync_NewMessage_ReturnsTrue()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        _store
            .TryLockAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(ci => ValueTask.FromResult(true));

        // Act
        bool result = await _sut.TryLockAsync(messageId, "TestConsumer", CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TryLockAsync_DuplicateMessage_ReturnsFalse()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        _store
            .TryLockAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(ci => ValueTask.FromResult(false));

        // Act
        bool result = await _sut.TryLockAsync(messageId, "TestConsumer", CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task TryLockAsync_ExpiredLock_ReturnsTrue()
    {
        // Arrange — store returns true when a previously-held lock has expired
        var messageId = Guid.NewGuid();
        _store
            .TryLockAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(ci => ValueTask.FromResult(true));

        // Act
        bool result = await _sut.TryLockAsync(messageId, "TestConsumer", CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TryLockAsync_DifferentMessageIds_BothTrue()
    {
        // Arrange
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        _store
            .TryLockAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(ci => ValueTask.FromResult(true));

        // Act
        bool firstResult = await _sut.TryLockAsync(firstId, "TestConsumer", CancellationToken.None);
        bool secondResult = await _sut.TryLockAsync(secondId, "TestConsumer", CancellationToken.None);

        // Assert
        firstResult.Should().BeTrue();
        secondResult.Should().BeTrue();
    }
}
