using System.Buffers;
using System.Text;
using System.Text.Json;

using BareWire.Abstractions.Exceptions;
using BareWire.Abstractions.Serialization;

namespace BareWire.Serialization.Json;

internal sealed class BareWireEnvelopeSerializer : IMessageSerializer, IMessageDeserializer
{
    public string ContentType => "application/vnd.barewire+json";

    public void Serialize<T>(T message, IBufferWriter<byte> output) where T : class
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(output);

        try
        {
            JsonElement body = JsonSerializer.SerializeToElement(message, BareWireJsonSerializerOptions.Default);

            var envelope = new BareWireEnvelope(
                MessageId: Guid.NewGuid(),
                CorrelationId: null,
                ConversationId: null,
                MessageType: [$"urn:message:{typeof(T).Namespace}:{typeof(T).Name}"],
                SentTime: DateTimeOffset.UtcNow,
                Headers: null,
                Body: body);

            using var writer = new Utf8JsonWriter(output, new JsonWriterOptions { SkipValidation = true });
            JsonSerializer.Serialize(writer, envelope, BareWireJsonSerializerOptions.Default);
        }
        catch (JsonException ex)
        {
            throw new BareWireSerializationException(
                $"Failed to serialize envelope for {typeof(T).Name}.",
                ContentType,
                targetType: typeof(T),
                innerException: ex);
        }
    }

    public T? Deserialize<T>(ReadOnlySequence<byte> data) where T : class
    {
        if (data.IsEmpty)
            return null;

        var reader = new Utf8JsonReader(data);
        try
        {
            BareWireEnvelope? envelope = JsonSerializer.Deserialize<BareWireEnvelope>(ref reader, BareWireJsonSerializerOptions.Default);

            if (envelope is null)
                return null;

            return envelope.Body.Deserialize<T>(BareWireJsonSerializerOptions.Default);
        }
        catch (JsonException ex)
        {
            throw new BareWireSerializationException(
                $"Failed to deserialize envelope for {typeof(T).Name}.",
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
