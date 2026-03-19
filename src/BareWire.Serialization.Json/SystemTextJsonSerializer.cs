using System.Buffers;
using System.Text.Json;

using BareWire.Abstractions.Exceptions;
using BareWire.Abstractions.Serialization;

namespace BareWire.Serialization.Json;

internal sealed class SystemTextJsonSerializer : IMessageSerializer
{
    public string ContentType => "application/json";

    public void Serialize<T>(T message, IBufferWriter<byte> output) where T : class
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(output);

        using var writer = new Utf8JsonWriter(output, new JsonWriterOptions { SkipValidation = true });
        try
        {
            JsonSerializer.Serialize(writer, message, BareWireJsonSerializerOptions.Default);
        }
        catch (JsonException ex)
        {
            throw new BareWireSerializationException(
                $"Failed to serialize {typeof(T).Name}.",
                ContentType,
                targetType: typeof(T),
                innerException: ex);
        }
    }
}
