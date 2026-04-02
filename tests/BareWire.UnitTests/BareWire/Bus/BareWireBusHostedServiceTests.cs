using AwesomeAssertions;
using BareWire.Abstractions;
using BareWire.Bus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace BareWire.UnitTests.Core.Bus;

public sealed class BareWireBusHostedServiceTests
{
    [Fact]
    public void Constructor_WhenBusControlIsNull_ThrowsArgumentNullException()
    {
        ILogger<BareWireBusHostedService> logger = NullLogger<BareWireBusHostedService>.Instance;

        Action act = () => _ = new BareWireBusHostedService(null!, logger);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("busControl");
    }

    [Fact]
    public void Constructor_WhenLoggerIsNull_ThrowsArgumentNullException()
    {
        IBusControl busControl = Substitute.For<IBusControl>();

        Action act = () => _ = new BareWireBusHostedService(busControl, null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }
}
