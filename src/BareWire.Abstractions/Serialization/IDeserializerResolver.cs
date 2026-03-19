namespace BareWire.Abstractions.Serialization;

/// <summary>
/// Resolves the appropriate <see cref="IMessageDeserializer"/> for a given MIME content type.
/// Implementations route inbound messages to the correct deserializer based on the content-type transport header.
/// </summary>
public interface IDeserializerResolver
{
    /// <summary>
    /// Returns the <see cref="IMessageDeserializer"/> registered for the specified <paramref name="contentType"/>.
    /// If no match is found, returns the default deserializer.
    /// </summary>
    /// <param name="contentType">The MIME content type from the transport header, or <see langword="null"/> for the default.</param>
    /// <returns>The matching deserializer. Never returns <see langword="null"/>.</returns>
    IMessageDeserializer Resolve(string? contentType);
}
