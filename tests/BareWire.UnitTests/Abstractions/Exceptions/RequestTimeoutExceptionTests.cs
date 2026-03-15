using AwesomeAssertions;
using BareWire.Abstractions.Exceptions;

namespace BareWire.UnitTests.Abstractions.Exceptions;

public sealed class RequestTimeoutExceptionTests
{
    private sealed record GetOrderRequest(Guid OrderId);

    [Fact]
    public void Constructor_SetsTimeoutProperties()
    {
        var requestType = typeof(GetOrderRequest);
        var timeout = TimeSpan.FromSeconds(30);
        var destination = new Uri("rabbitmq://localhost/get-order");

        var ex = new RequestTimeoutException(requestType, timeout, destination, "RabbitMQ");

        ex.RequestType.Should().Be(requestType);
        ex.Timeout.Should().Be(timeout);
        ex.DestinationAddress.Should().Be(destination);
        ex.TransportName.Should().Be("RabbitMQ");
        ex.Message.Should().Contain("GetOrderRequest");
        ex.Message.Should().Contain("30");
    }

    [Fact]
    public void InheritsFromBareWireTransportException()
    {
        var ex = new RequestTimeoutException(typeof(GetOrderRequest), TimeSpan.FromSeconds(5), null);

        ex.Should().BeAssignableTo<BareWireTransportException>();
    }
}
