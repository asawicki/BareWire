namespace BareWire.Abstractions.Saga;

/// <summary>
/// Configures the scheduling parameters for a timed event within a <see cref="BareWireStateMachine{TSaga}"/>.
/// </summary>
public interface IScheduleConfigurator
{
    /// <summary>
    /// Gets or sets the delay after which the scheduled message is delivered.
    /// </summary>
    TimeSpan Delay { get; set; }

    /// <summary>
    /// Gets or sets the strategy used to implement the delay.
    /// </summary>
    SchedulingStrategy Strategy { get; set; }
}
