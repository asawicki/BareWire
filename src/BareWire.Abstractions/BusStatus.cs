namespace BareWire.Abstractions;

/// <summary>
/// Represents the operational health status of a BareWire bus instance.
/// </summary>
public enum BusStatus
{
    /// <summary>
    /// The bus and all its endpoints are operating normally with no detected issues.
    /// </summary>
    Healthy,

    /// <summary>
    /// The bus is operational but one or more endpoints are experiencing issues that may affect reliability.
    /// </summary>
    Degraded,

    /// <summary>
    /// The bus or a critical endpoint has failed and is not able to process messages.
    /// </summary>
    Unhealthy,
}
