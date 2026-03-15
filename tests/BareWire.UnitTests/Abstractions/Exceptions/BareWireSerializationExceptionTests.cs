using AwesomeAssertions;
using BareWire.Abstractions.Exceptions;

namespace BareWire.UnitTests.Abstractions.Exceptions;

public sealed class BareWireSerializationExceptionTests
{
    private sealed record OrderCreated(Guid OrderId);

    [Fact]
    public void Constructor_SetsSerializationProperties()
    {
        const string contentType = "application/json";
        var targetType = typeof(OrderCreated);
        const string rawPayload = """{"OrderId":"abc"}""";

        var ex = new BareWireSerializationException("Failed to deserialize.", contentType, targetType, rawPayload);

        ex.ContentType.Should().Be(contentType);
        ex.TargetType.Should().Be(targetType);
        ex.RawPayload.Should().Be(rawPayload);
        ex.Message.Should().Be("Failed to deserialize.");
    }

    [Fact]
    public void RawPayload_IsTruncated()
    {
        var longPayload = new string('x', 600);

        var ex = new BareWireSerializationException("error", "application/json", rawPayload: longPayload);

        ex.RawPayload.Should().NotBeNull();
        ex.RawPayload!.Length.Should().BeLessThan(longPayload.Length);
        ex.RawPayload.Should().Contain("[truncated]");
        ex.RawPayload!.Length.Should().Be(BareWireSerializationException.MaxRawPayloadLength + "[truncated]".Length);
    }

    [Fact]
    public void InheritsFromBareWireException()
    {
        var ex = new BareWireSerializationException("error", "application/json");

        ex.Should().BeAssignableTo<BareWireException>();
    }
}
