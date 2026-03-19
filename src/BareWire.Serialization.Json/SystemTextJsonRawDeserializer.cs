using System.Buffers;
using System.Text;
using System.Text.Json;

using BareWire.Abstractions.Exceptions;
using BareWire.Abstractions.Serialization;

namespace BareWire.Serialization.Json;

internal sealed class SystemTextJsonRawDeserializer : IMessageDeserializer
{
    public string ContentType => "application/json";

    public T? Deserialize<T>(ReadOnlySequence<byte> data) where T : class
    {
        if (data.IsEmpty)
            return null;

        var reader = new Utf8JsonReader(data);
        try
        {
            return JsonSerializer.Deserialize<T>(ref reader, BareWireJsonSerializerOptions.Default);
        }
        catch (JsonException ex)
        {
            throw new BareWireSerializationException(
                $"Failed to deserialize {typeof(T).Name} from raw JSON.",
                ContentType,
                targetType: typeof(T),
                rawPayload: ExtractRawPayload(data),
                innerException: ex);
        }
    }

    private static string? ExtractRawPayload(ReadOnlySequence<byte> data)
    {
        const int maxBytes = BareWireSerializationException.MaxRawPayloadLength;

        if (data.Length <= maxBytes)
        {
            return data.IsSingleSegment
                ? Encoding.UTF8.GetString(data.FirstSpan)
                : Encoding.UTF8.GetString(data);
        }

        ReadOnlySequence<byte> slice = data.Slice(0, maxBytes);
        return slice.IsSingleSegment
            ? Encoding.UTF8.GetString(slice.FirstSpan)
            : Encoding.UTF8.GetString(slice);
    }
}
