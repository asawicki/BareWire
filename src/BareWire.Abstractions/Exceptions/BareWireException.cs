namespace BareWire.Abstractions.Exceptions;

/// <summary>
/// The base exception for all errors originating from the BareWire library.
/// Catch this type to handle any BareWire-specific failure in a single handler.
/// </summary>
/// <remarks>
/// This class is intentionally not sealed so that the transport, serialization, saga,
/// and configuration subsystems can each define their own specific exception hierarchy.
/// Application code should catch the most-derived type available.
/// </remarks>
public class BareWireException : Exception
{
    /// <summary>
    /// Initializes a new instance of <see cref="BareWireException"/> with the specified error message.
    /// </summary>
    /// <param name="message">A human-readable description of the error.</param>
    public BareWireException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of <see cref="BareWireException"/> with the specified error message
    /// and a reference to the inner exception that caused this exception.
    /// </summary>
    /// <param name="message">A human-readable description of the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public BareWireException(string message, Exception innerException)
        : base(message, innerException) { }
}
