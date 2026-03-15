namespace BareWire.Abstractions;

/// <summary>
/// Represents the aggregated health status of a bus instance and all its receive endpoints.
/// Returned by <see cref="IBusControl.CheckHealth"/>.
/// </summary>
/// <param name="Status">The overall health status of the bus.</param>
/// <param name="Description">A human-readable description of the current health state.</param>
/// <param name="Endpoints">The per-endpoint health statuses. Never null.</param>
public sealed record BusHealthStatus(
    BusStatus Status,
    string Description,
    IReadOnlyList<EndpointHealthStatus> Endpoints);

/// <summary>
/// Represents the health status of a single receive endpoint within a bus instance.
/// </summary>
/// <param name="EndpointName">The name (queue name) of the receive endpoint.</param>
/// <param name="Status">The health status of this endpoint.</param>
/// <param name="Description">
/// An optional human-readable description of the current state, e.g. an error message.
/// <see langword="null"/> when the endpoint is healthy and no additional context is needed.
/// </param>
public sealed record EndpointHealthStatus(
    string EndpointName,
    BusStatus Status,
    string? Description);
