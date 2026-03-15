using AwesomeAssertions;
using BareWire.Abstractions.Exceptions;

namespace BareWire.UnitTests.Abstractions.Exceptions;

public sealed class ConcurrencyExceptionTests
{
    private sealed class PaymentSaga { }

    [Fact]
    public void Constructor_SetsVersions()
    {
        var sagaType = typeof(PaymentSaga);
        var correlationId = Guid.NewGuid();
        const int expectedVersion = 3;
        const int actualVersion = 5;

        var ex = new ConcurrencyException(sagaType, correlationId, expectedVersion, actualVersion);

        ex.ExpectedVersion.Should().Be(expectedVersion);
        ex.ActualVersion.Should().Be(actualVersion);
        ex.SagaType.Should().Be(sagaType);
        ex.CorrelationId.Should().Be(correlationId);
        ex.Message.Should().Contain("PaymentSaga");
        ex.Message.Should().Contain(expectedVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
        ex.Message.Should().Contain(actualVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void InheritsFromBareWireSagaException()
    {
        var ex = new ConcurrencyException(typeof(PaymentSaga), Guid.NewGuid(), expectedVersion: 1, actualVersion: 2);

        ex.Should().BeAssignableTo<BareWireSagaException>();
    }
}
