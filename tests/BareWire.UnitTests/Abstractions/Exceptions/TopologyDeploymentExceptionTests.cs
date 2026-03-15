using AwesomeAssertions;
using BareWire.Abstractions.Exceptions;

namespace BareWire.UnitTests.Abstractions.Exceptions;

public sealed class TopologyDeploymentExceptionTests
{
    [Fact]
    public void Constructor_SetsTopologyProperties()
    {
        const string topologyElement = "orders-exchange";
        const string brokerError = "PRECONDITION_FAILED - inequivalent arg 'type'";

        var ex = new TopologyDeploymentException(topologyElement, "RabbitMQ", brokerError);

        ex.TopologyElement.Should().Be(topologyElement);
        ex.BrokerError.Should().Be(brokerError);
        ex.TransportName.Should().Be("RabbitMQ");
        ex.Message.Should().Contain(topologyElement);
        ex.Message.Should().Contain(brokerError);
    }

    [Fact]
    public void InheritsFromBareWireTransportException()
    {
        var ex = new TopologyDeploymentException("my-queue");

        ex.Should().BeAssignableTo<BareWireTransportException>();
    }
}
