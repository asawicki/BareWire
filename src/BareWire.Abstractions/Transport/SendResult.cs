namespace BareWire.Abstractions.Transport;

/// <summary>
/// Represents the result of a transport-level send operation, capturing whether
/// the broker confirmed receipt and the associated delivery tag.
/// </summary>
/// <param name="IsConfirmed">
/// Indicates whether the broker confirmed durable receipt of the message (publisher confirms).
/// <c>true</c> when a positive acknowledgement was received; <c>false</c> when unconfirmed or negatively acknowledged.
/// </param>
/// <param name="DeliveryTag">
/// The transport-specific delivery tag assigned to this send operation.
/// Zero when the transport does not support publisher confirms.
/// </param>
public readonly record struct SendResult(bool IsConfirmed, ulong DeliveryTag);
