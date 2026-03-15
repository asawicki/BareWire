using AwesomeAssertions;
using BareWire.Abstractions.Exceptions;

namespace BareWire.UnitTests.Abstractions.Exceptions;

public sealed class BareWireExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_SetsMessage()
    {
        const string message = "Something went wrong in BareWire.";

        var ex = new BareWireException(message);

        ex.Message.Should().Be(message);
    }

    [Fact]
    public void Constructor_WithInnerException_SetsInnerException()
    {
        var inner = new InvalidOperationException("inner cause");

        var ex = new BareWireException("outer message", inner);

        ex.InnerException.Should().NotBeNull();
        ex.InnerException.Should().Be(inner);
    }
}
