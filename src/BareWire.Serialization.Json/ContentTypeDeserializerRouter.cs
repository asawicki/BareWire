using BareWire.Abstractions.Serialization;

namespace BareWire.Serialization.Json;

/// <summary>
/// Routes inbound messages to the appropriate <see cref="IMessageDeserializer"/>
/// based on the MIME content type from the transport header.
/// Falls back to the default deserializer when no match is found.
/// </summary>
internal sealed class ContentTypeDeserializerRouter : IDeserializerResolver
{
    private readonly Dictionary<string, IMessageDeserializer> _deserializers;
    private readonly IMessageDeserializer _defaultDeserializer;

    internal ContentTypeDeserializerRouter(
        IMessageDeserializer defaultDeserializer,
        IEnumerable<IMessageDeserializer>? additionalDeserializers = null)
    {
        ArgumentNullException.ThrowIfNull(defaultDeserializer);

        _defaultDeserializer = defaultDeserializer;
        _deserializers = new Dictionary<string, IMessageDeserializer>(StringComparer.OrdinalIgnoreCase);

        // Register the default deserializer under its own content type
        _deserializers[defaultDeserializer.ContentType] = defaultDeserializer;

        // Register additional deserializers
        if (additionalDeserializers is not null)
        {
            foreach (var deserializer in additionalDeserializers)
            {
                _deserializers[deserializer.ContentType] = deserializer;
            }
        }
    }

    public IMessageDeserializer Resolve(string? contentType)
    {
        if (contentType is null || !_deserializers.TryGetValue(contentType, out var deserializer))
        {
            return _defaultDeserializer;
        }

        return deserializer;
    }
}
