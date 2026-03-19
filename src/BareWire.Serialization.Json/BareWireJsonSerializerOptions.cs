using System.Text.Json;
using System.Text.Json.Serialization;

namespace BareWire.Serialization.Json;

internal static class BareWireJsonSerializerOptions
{
    internal static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };
}
