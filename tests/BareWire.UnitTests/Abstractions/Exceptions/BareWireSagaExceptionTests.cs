using AwesomeAssertions;
using BareWire.Abstractions.Exceptions;

namespace BareWire.UnitTests.Abstractions.Exceptions;

public sealed class BareWireSagaExceptionTests
{
    private sealed class OrderSaga { }

    [Fact]
    public void Constructor_SetsSagaProperties()
    {
        var sagaType = typeof(OrderSaga);
        var correlationId = Guid.NewGuid();
        const string currentState = "AwaitingPayment";

        var ex = new BareWireSagaException("Saga failed.", sagaType, correlationId, currentState);

        ex.SagaType.Should().Be(sagaType);
        ex.CorrelationId.Should().Be(correlationId);
        ex.CurrentState.Should().Be(currentState);
        ex.Message.Should().Be("Saga failed.");
    }

    [Fact]
    public void InheritsFromBareWireException()
    {
        var ex = new BareWireSagaException("msg", typeof(OrderSaga), Guid.NewGuid());

        ex.Should().BeAssignableTo<BareWireException>();
    }
}
