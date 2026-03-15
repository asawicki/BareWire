using AwesomeAssertions;
using BareWire.Abstractions.Exceptions;

namespace BareWire.UnitTests.Abstractions.Exceptions;

public sealed class UnknownPayloadExceptionTests
{
    [Fact]
    public void Constructor_SetsPayloadProperties()
    {
        const string endpointName = "orders-input";
        const string contentType = "application/avro";

        var ex = new UnknownPayloadException(endpointName, contentType);

        ex.EndpointName.Should().Be(endpointName);
        ex.ContentType.Should().Be(contentType);
        ex.Message.Should().Contain(endpointName);
        ex.Message.Should().Contain(contentType);
    }

    [Fact]
    public void InheritsFromBareWireException()
    {
        var ex = new UnknownPayloadException("my-endpoint");

        ex.Should().BeAssignableTo<BareWireException>();
    }
}
