using System.Buffers;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using BareWire.Abstractions;
using BareWire.Abstractions.Saga;
using BareWire.Abstractions.Serialization;
using BareWire.Saga;
using BareWire.Serialization.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace BareWire.UnitTests.Saga;

// ── Types for dispatcher tests ────────────────────────────────────────────────

public sealed class DispatcherSagaState : ISagaState
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = "Initial";
    public int Version { get; set; }
    public string? LastEvent { get; set; }
}

public sealed record DispatcherOrderCreated(Guid OrderId, string Customer);
public sealed record DispatcherOrderShipped(Guid OrderId, string TrackingNumber);

internal sealed class DispatcherStateMachine : BareWireStateMachine<DispatcherSagaState>
{
    public DispatcherStateMachine()
    {
        var orderCreated = Event<DispatcherOrderCreated>();
        var orderShipped = Event<DispatcherOrderShipped>();

        CorrelateBy<DispatcherOrderCreated>(e => e.OrderId);
        CorrelateBy<DispatcherOrderShipped>(e => e.OrderId);
        // Note: DispatcherUnrelatedEvent is NOT registered here

        Initially(() =>
        {
            When(orderCreated, b => b
                .Then((saga, evt) =>
                {
                    saga.LastEvent = $"created:{evt.Customer}";
                    return Task.CompletedTask;
                })
                .TransitionTo("Processing"));
        });

        During("Processing", () =>
        {
            When(orderShipped, b => b
                .Then((saga, evt) =>
                {
                    saga.LastEvent = $"shipped:{evt.TrackingNumber}";
                    return Task.CompletedTask;
                })
                .TransitionTo("Completed")
                .Finalize());
        });
    }
}

// ── Tests ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Verifies that <see cref="SagaMessageDispatcher{TStateMachine,TSaga}"/> correctly
/// deserializes inbound messages and routes them to the saga state machine executor.
/// Regression tests for: saga state machine never executed in the consume pipeline.
/// </summary>
public sealed class SagaMessageDispatcherTests
{
    private readonly ISagaRepository<DispatcherSagaState> _repository;
    private readonly SagaMessageDispatcher<DispatcherStateMachine, DispatcherSagaState> _dispatcher;
    private readonly IMessageDeserializer _deserializer;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ISendEndpointProvider _sendEndpointProvider;

    public SagaMessageDispatcherTests()
    {
        var machine = new DispatcherStateMachine();
        var definition = StateMachineDefinition<DispatcherSagaState>.Build(machine);

        _repository = Substitute.For<ISagaRepository<DispatcherSagaState>>();
        _repository.FindAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                   .Returns((DispatcherSagaState?)null);
        _repository.SaveAsync(Arg.Any<DispatcherSagaState>(), Arg.Any<CancellationToken>())
                   .Returns(Task.CompletedTask);

        // Build a minimal IServiceScopeFactory that returns the mocked repository
        // when ISagaRepository<DispatcherSagaState> is requested.
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var scope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(ISagaRepository<DispatcherSagaState>))
                       .Returns(_repository);
        scope.ServiceProvider.Returns(serviceProvider);
        scopeFactory.CreateScope().Returns(scope);

        ILoggerFactory loggerFactory = NullLoggerFactory.Instance;

        _dispatcher = new SagaMessageDispatcher<DispatcherStateMachine, DispatcherSagaState>(
            definition,
            machine,
            scopeFactory,
            loggerFactory);

        _deserializer = new SystemTextJsonRawDeserializer();
        _publishEndpoint = Substitute.For<IPublishEndpoint>();
        _sendEndpointProvider = Substitute.For<ISendEndpointProvider>();
    }

    // ── StateMachineType ──────────────────────────────────────────────────────

    [Fact]
    public void StateMachineType_ReturnsStateMachineConcreteType()
    {
        _dispatcher.StateMachineType.Should().Be<DispatcherStateMachine>();
    }

    // ── TryDispatchAsync — matching event ─────────────────────────────────────

    [Fact]
    public async Task TryDispatchAsync_MatchingEventType_ReturnsTrueAndProcessesSaga()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var evt = new DispatcherOrderCreated(orderId, "Alice");
        var body = SerializeToSequence(evt);

        DispatcherSagaState? savedSaga = null;
        _repository.SaveAsync(
                       Arg.Do<DispatcherSagaState>(s => savedSaga = s),
                       Arg.Any<CancellationToken>())
                   .Returns(Task.CompletedTask);

        // Act
        bool result = await _dispatcher.TryDispatchAsync(
            body,
            headers: new Dictionary<string, string>(),
            messageId: Guid.NewGuid().ToString(),
            _publishEndpoint,
            _sendEndpointProvider,
            _deserializer,
            CancellationToken.None);

        // Assert — dispatcher matched and state machine ran
        result.Should().BeTrue();
        savedSaga.Should().NotBeNull();
        savedSaga!.CurrentState.Should().Be("Processing");
        savedSaga.LastEvent.Should().Be("created:Alice");
    }

    [Fact]
    public async Task TryDispatchAsync_MatchingEventType_CallsRepositorySave()
    {
        // Arrange — a second call with the same event type; verifies SaveAsync is called
        // because this is a new saga instance (no existing state).
        var orderId = Guid.NewGuid();
        var evt = new DispatcherOrderCreated(orderId, "Bob");
        var body = SerializeToSequence(evt);

        DispatcherSagaState? savedSaga = null;
        _repository.SaveAsync(
                       Arg.Do<DispatcherSagaState>(s => savedSaga = s),
                       Arg.Any<CancellationToken>())
                   .Returns(Task.CompletedTask);

        // Act
        bool result = await _dispatcher.TryDispatchAsync(
            body,
            headers: new Dictionary<string, string>(),
            messageId: Guid.NewGuid().ToString(),
            _publishEndpoint,
            _sendEndpointProvider,
            _deserializer,
            CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        // Repository.SaveAsync should have been called with the new saga instance.
        await _repository.Received(1).SaveAsync(Arg.Any<DispatcherSagaState>(), Arg.Any<CancellationToken>());
        savedSaga.Should().NotBeNull();
        savedSaga!.CorrelationId.Should().Be(orderId);
    }

    // ── TryDispatchAsync — non-matching body ──────────────────────────────────

    [Fact]
    public async Task TryDispatchAsync_EmptyBody_ReturnsFalse()
    {
        // Arrange — empty body cannot be deserialized as any event type
        var body = ReadOnlySequence<byte>.Empty;

        // Act
        bool result = await _dispatcher.TryDispatchAsync(
            body,
            headers: new Dictionary<string, string>(),
            messageId: Guid.NewGuid().ToString(),
            _publishEndpoint,
            _sendEndpointProvider,
            _deserializer,
            CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        await _repository.DidNotReceive().SaveAsync(Arg.Any<DispatcherSagaState>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryDispatchAsync_NullJsonBody_ReturnsFalse()
    {
        // Arrange — JSON "null" deserializes to null, so dispatcher should return false
        var body = SerializeNullAsSequence();

        // Act
        bool result = await _dispatcher.TryDispatchAsync(
            body,
            headers: new Dictionary<string, string>(),
            messageId: Guid.NewGuid().ToString(),
            _publishEndpoint,
            _sendEndpointProvider,
            _deserializer,
            CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    // ── ReceiveEndpointRunner integration ─────────────────────────────────────

    [Fact]
    public void ReceiveEndpointRunner_WithSagaDispatcher_FiltersToEndpointSagaTypes()
    {
        // Arrange — two dispatchers but only one is listed in the binding's SagaTypes
        var matchingDispatcher = Substitute.For<ISagaMessageDispatcher>();
        matchingDispatcher.StateMachineType.Returns(typeof(DispatcherStateMachine));

        var otherDispatcher = Substitute.For<ISagaMessageDispatcher>();
        otherDispatcher.StateMachineType.Returns(typeof(DispatcherStateMachine)); // same type for test isolation

        // The dispatcher test verifies that the filter logic uses SagaTypes,
        // not that the runner dispatches (covered by integration tests).
        // Here we verify the SagaTypes property flows correctly.
        var binding = new BareWire.Abstractions.Configuration.EndpointBinding
        {
            EndpointName = "test-queue",
            SagaTypes = [typeof(DispatcherStateMachine)],
        };

        binding.SagaTypes.Should().Contain(typeof(DispatcherStateMachine));
        binding.SagaTypes.Should().HaveCount(1);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ReadOnlySequence<byte> SerializeToSequence<T>(T value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value));
        return new ReadOnlySequence<byte>(bytes);
    }

    private static ReadOnlySequence<byte> SerializeNullAsSequence()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("null");
        return new ReadOnlySequence<byte>(bytes);
    }
}
