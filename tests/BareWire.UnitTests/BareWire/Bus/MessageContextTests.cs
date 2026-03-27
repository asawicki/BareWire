using System.Buffers;
using AwesomeAssertions;
using BareWire.Abstractions.Pipeline;
using NSubstitute;

namespace BareWire.UnitTests.Core.Bus;

public sealed class MessageContextTests
{
    [Fact]
    public void MessageContext_LegacyConstructor_SetsEmptyEndpointName()
    {
        // Arrange & Act
        var context = new MessageContext(
            Guid.NewGuid(),
            new Dictionary<string, string>(),
            ReadOnlySequence<byte>.Empty,
            Substitute.For<IServiceProvider>());

        // Assert
        context.EndpointName.Should().BeEmpty();
    }

    [Fact]
    public void MessageContext_NewConstructor_SetsEndpointName()
    {
        // Arrange & Act
        var context = new MessageContext(
            Guid.NewGuid(),
            new Dictionary<string, string>(),
            ReadOnlySequence<byte>.Empty,
            Substitute.For<IServiceProvider>(),
            endpointName: "my-endpoint");

        // Assert
        context.EndpointName.Should().Be("my-endpoint");
    }
}
