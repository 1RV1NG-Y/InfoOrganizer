using System.Text.Json;
using System.Text.Json.Serialization;

namespace InfoOrganizer.Domain;

/// <summary>Single source of truth for (de)serializing mapping data stored on a <see cref="SourceProfile"/>.
/// Enums are written as names so saved profiles stay readable and stable across releases.</summary>
public static class MappingSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static string SerializeFields(IEnumerable<FieldMapping> fields) =>
        JsonSerializer.Serialize(fields, Options);

    public static List<FieldMapping> DeserializeFields(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? new List<FieldMapping>()
            : JsonSerializer.Deserialize<List<FieldMapping>>(json, Options) ?? new List<FieldMapping>();

    public static string SerializeHints(MappingHints hints) => JsonSerializer.Serialize(hints, Options);

    public static MappingHints DeserializeHints(string? json)
    {
        var hints = string.IsNullOrWhiteSpace(json)
            ? new MappingHints()
            : JsonSerializer.Deserialize<MappingHints>(json, Options) ?? new MappingHints();

        if (string.IsNullOrWhiteSpace(hints.DefaultCurrency))
            hints.DefaultCurrency = "MXN";

        return hints;
    }
}
