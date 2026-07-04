using System.Text.Json;
using InfoOrganizer.Domain;

namespace InfoOrganizer.Ai;

internal static class AiResponseParser
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static MappingProposal ParseProposal(string json, RawTable table)
    {
        var dto = JsonSerializer.Deserialize<MappingDto>(json, JsonOpts)
            ?? throw new InvalidOperationException("Could not parse mapping JSON.");

        var columns = new HashSet<string>(table.ColumnNames, StringComparer.OrdinalIgnoreCase);
        var fields = new List<FieldMapping>();
        foreach (var f in dto.Fields ?? new())
        {
            if (!Enum.TryParse<CanonicalField>(f.Field, ignoreCase: true, out var field)) continue;
            var col = string.IsNullOrWhiteSpace(f.SourceColumn) || !columns.Contains(f.SourceColumn)
                ? null
                : table.Columns.First(c => c.Name.Equals(f.SourceColumn, StringComparison.OrdinalIgnoreCase)).Name;
            fields.Add(new FieldMapping { Field = field, SourceColumn = col, Confidence = Math.Clamp(f.Confidence, 0, 1) });
        }

        Enum.TryParse<RecordType>(dto.RecordType, ignoreCase: true, out var recordType);
        var hints = new MappingHints
        {
            DateFormat = string.IsNullOrWhiteSpace(dto.Hints?.DateFormat) ? null : dto.Hints!.DateFormat,
            DecimalComma = dto.Hints?.DecimalComma ?? false,
            DefaultCurrency = string.IsNullOrWhiteSpace(dto.Hints?.DefaultCurrency) ? null : dto.Hints!.DefaultCurrency
        };

        return new MappingProposal
        {
            Fields = fields,
            DetectedRecordType = recordType,
            Hints = hints,
            OverallConfidence = fields.Where(f => f.SourceColumn != null).Select(f => f.Confidence).DefaultIfEmpty(0).Average(),
            Rationale = dto.Rationale
        };
    }

    public static RawTable ParseImageTable(string json, string fileName)
    {
        var dto = JsonSerializer.Deserialize<ImageDto>(json, JsonOpts)
            ?? throw new InvalidOperationException("Could not parse extracted table JSON.");

        var table = new RawTable
        {
            Meta = new SourceMeta { SourceType = SourceType.Image, FileName = fileName, Notes = dto.Notes }
        };

        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int blank = 0;
        foreach (var raw in dto.Columns ?? new())
        {
            var name = string.IsNullOrWhiteSpace(raw) ? $"Column{++blank}" : raw.Trim();
            if (seen.TryGetValue(name, out var n)) { seen[name] = ++n; name = $"{name} ({n})"; }
            else seen[name] = 1;
            table.Columns.Add(new RawColumn { Name = name });
        }

        int index = 0;
        foreach (var cells in dto.Rows ?? new())
        {
            var row = new RawRow { Index = index++ };
            for (int c = 0; c < table.Columns.Count; c++)
                row.Cells[table.Columns[c].Name] = c < cells.Count ? cells[c] : null;
            if (row.Cells.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
                table.Rows.Add(row);
        }

        return table;
    }

    private sealed record ImageDto(List<string>? Columns, List<List<string>>? Rows, string? Notes);

    private sealed record MappingDto(List<FieldDto>? Fields, string RecordType, HintsDto? Hints, string? Rationale);
    private sealed record FieldDto(string Field, string? SourceColumn, double Confidence);
    private sealed record HintsDto(string? DateFormat, bool DecimalComma, string? DefaultCurrency);
}
