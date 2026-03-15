using AwesomeAssertions;
using BareWire.Abstractions.Exceptions;

namespace BareWire.UnitTests.Abstractions.Exceptions;

public sealed class BareWireTransportExceptionTests
{
    [Fact]
    public void Constructor_SetsTransportProperties()
    {
        const string transportName = "RabbitMQ";
        var endpointAddress = new Uri("amqp://localhost:5672/orders");
        var inner = new IOException("connection reset");

        var ex = new BareWireTransportException("Transport failed.", transportName, endpointAddress, inner);

        ex.TransportName.Should().Be(transportName);
        ex.EndpointAddress.Should().Be(endpointAddress);
        ex.InnerException.Should().Be(inner);
        ex.Message.Should().Be("Transport failed.");
    }

    [Fact]
    public void InheritsFromBareWireException()
    {
        var ex = new BareWireTransportException("msg", "RabbitMQ", endpointAddress: null);

        ex.Should().BeAssignableTo<BareWireException>();
    }
}
