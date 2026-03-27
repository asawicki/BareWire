using AwesomeAssertions;
using BareWire.Outbox;

namespace BareWire.UnitTests.Outbox;

public sealed class InMemoryInboxStoreTests
{
    [Fact]
    public async Task InMemoryInboxStore_MarkProcessed_PreventsRelock()
    {
        // Arrange
        var store = new InMemoryInboxStore();
        Guid messageId = Guid.NewGuid();
        const string consumerType = "TestConsumer";

        // Lock and mark as processed
        bool locked = await store.TryLockAsync(messageId, consumerType, TimeSpan.FromMilliseconds(1), CancellationToken.None);
        locked.Should().BeTrue();

        await store.MarkProcessedAsync(messageId, consumerType, CancellationToken.None);

        // Wait for the lock to expire
        await Task.Delay(10);

        // Act — try to relock after expiry
        bool relocked = await store.TryLockAsync(messageId, consumerType, TimeSpan.FromMinutes(5), CancellationToken.None);

        // Assert — must be denied: processed entries are permanently locked
        relocked.Should().BeFalse("a processed entry must not be re-locked even after the lock expires");
    }

    [Fact]
    public async Task InMemoryInboxStore_ExpiredUnprocessedLock_CanBeReacquired()
    {
        // Arrange
        var store = new InMemoryInboxStore();
        Guid messageId = Guid.NewGuid();
        const string consumerType = "TestConsumer";

        bool locked = await store.TryLockAsync(messageId, consumerType, TimeSpan.FromMilliseconds(1), CancellationToken.None);
        locked.Should().BeTrue();

        // Do NOT mark as processed — simulate a crashed handler that never committed
        await Task.Delay(10);

        // Act — try to relock after expiry
        bool relocked = await store.TryLockAsync(messageId, consumerType, TimeSpan.FromMinutes(5), CancellationToken.None);

        // Assert — should succeed: expired but unprocessed lock is eligible for re-acquisition
        relocked.Should().BeTrue("an expired lock that was never marked processed must be re-acquirable");
    }
}
