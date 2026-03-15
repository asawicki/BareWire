namespace BareWire.Abstractions.Exceptions;

/// <summary>
/// Thrown when a BareWire configuration option has an invalid or missing value.
/// Provides the option name, the supplied value (if any), and the expected value or constraint
/// so that the caller can diagnose the misconfiguration without consulting documentation.
/// </summary>
public sealed class BareWireConfigurationException : BareWireException
{
    /// <summary>
    /// Gets the name of the configuration option that caused the error.
    /// </summary>
    public string OptionName { get; }

    /// <summary>
    /// Gets the value that was supplied for the option, or <see langword="null"/> when no value was provided.
    /// </summary>
    public string? OptionValue { get; }

    /// <summary>
    /// Gets a description of the expected value or constraint for the option,
    /// or <see langword="null"/> when no constraint description is available.
    /// </summary>
    public string? ExpectedValue { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="BareWireConfigurationException"/>.
    /// </summary>
    /// <param name="optionName">The name of the misconfigured option. Must not be null.</param>
    /// <param name="optionValue">The value that was supplied, or <see langword="null"/> if absent.</param>
    /// <param name="expectedValue">A description of the expected value or constraint, or <see langword="null"/>.</param>
    /// <param name="innerException">An optional inner exception that caused this configuration failure.</param>
    public BareWireConfigurationException(
        string optionName,
        string? optionValue = null,
        string? expectedValue = null,
        Exception? innerException = null)
        : base(BuildMessage(optionName, optionValue, expectedValue), innerException!)
    {
        OptionName = optionName ?? throw new ArgumentNullException(nameof(optionName));
        OptionValue = optionValue;
        ExpectedValue = expectedValue;
    }

    private static string BuildMessage(string optionName, string? optionValue, string? expectedValue)
    {
        var supplied = optionValue is not null ? $" Supplied value: '{optionValue}'." : string.Empty;
        var expected = expectedValue is not null ? $" Expected: {expectedValue}." : string.Empty;
        return $"BareWire configuration error for option '{optionName}'.{supplied}{expected}";
    }
}
