using System.Text.Json;
using InfoOrganizer.Domain;

namespace InfoOrganizer.Ai;

internal static class AiSchemas
{
    public static string MappingSchemaJson
    {
        get
        {
            var canonical = string.Join(",", CanonicalSchema.Fields.Select(f => $"\"{f.Field}\""));
            var recordTypes = string.Join(",", Enum.GetNames<RecordType>().Select(n => $"\"{n}\""));
            return $$"""
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "fields": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "additionalProperties": false,
                    "properties": {
                      "field": { "type": "string", "enum": [{{canonical}}] },
                      "sourceColumn": { "type": "string" },
                      "confidence": { "type": "number" }
                    },
                    "required": ["field", "sourceColumn", "confidence"]
                  }
                },
                "recordType": { "type": "string", "enum": [{{recordTypes}}] },
                "hints": {
                  "type": "object",
                  "additionalProperties": false,
                  "properties": {
                    "dateFormat": { "type": "string" },
                    "decimalComma": { "type": "boolean" },
                    "defaultCurrency": { "type": "string" }
                  },
                  "required": ["dateFormat", "decimalComma", "defaultCurrency"]
                },
                "rationale": { "type": "string" }
              },
              "required": ["fields", "recordType", "hints", "rationale"]
            }
            """;
        }
    }

    public const string ImageSchemaJson = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "columns": { "type": "array", "items": { "type": "string" } },
            "rows": { "type": "array", "items": { "type": "array", "items": { "type": "string" } } },
            "notes": { "type": "string" }
          },
          "required": ["columns", "rows", "notes"]
        }
        """;

    public static Dictionary<string, JsonElement> MappingSchema() =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(MappingSchemaJson)!;

    public static Dictionary<string, JsonElement> ImageSchema() =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(ImageSchemaJson)!;
}
