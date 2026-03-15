using AwesomeAssertions;
using BareWire.Abstractions.Exceptions;

namespace BareWire.UnitTests.Abstractions.Exceptions;

public sealed class BareWireConfigurationExceptionTests
{
    [Fact]
    public void Constructor_SetsContextualProperties()
    {
        const string optionName = "MaxInFlightMessages";
        const string optionValue = "-1";
        const string expectedValue = "a positive integer";

        var ex = new BareWireConfigurationException(optionName, optionValue, expectedValue);

        ex.OptionName.Should().Be(optionName);
        ex.OptionValue.Should().Be(optionValue);
        ex.ExpectedValue.Should().Be(expectedValue);
    }

    [Fact]
    public void Message_ContainsOptionDetails()
    {
        const string optionName = "TransportUri";
        const string optionValue = "not-a-uri";
        const string expectedValue = "a valid absolute URI";

        var ex = new BareWireConfigurationException(optionName, optionValue, expectedValue);

        ex.Message.Should().Contain(optionName);
        ex.Message.Should().Contain(optionValue);
        ex.Message.Should().Contain(expectedValue);
    }
}
