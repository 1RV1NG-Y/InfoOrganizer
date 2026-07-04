using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using InfoOrganizer.Domain;

namespace InfoOrganizer.Ai;

public sealed class AnthropicOptions
{
    public string Model { get; set; } = "claude-opus-4-8";
}

/// <summary>Real <see cref="IAiClient"/> backed by the Anthropic C# SDK. Uses structured outputs so the
/// model returns strict JSON for both column mapping and (in P5) photo table extraction.</summary>
public sealed class AnthropicAiClient : IAiClient
{
    private const int MaxSampleRows = 8;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly IAnthropicApiKeyProvider _keyProvider;
    private readonly object _gate = new();
    private AnthropicClient? _client;
    private string? _clientKey;

    public AnthropicAiClient(AnthropicOptions options, IAnthropicApiKeyProvider keyProvider)
    {
        _keyProvider = keyProvider;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_keyProvider.GetApiKey());

    public async Task<MappingProposal> ProposeMappingAsync(RawTable table, CancellationToken ct = default)
    {
        var client = GetClient();

        var parameters = new MessageCreateParams
        {
            Model = Model.ClaudeOpus4_8,
            MaxTokens = 8000,
            Thinking = new ThinkingConfigAdaptive(),
            OutputConfig = new OutputConfig
            {
                Effort = Effort.Medium,
                Format = new JsonOutputFormat { Schema = MappingSchema() }
            },
            System = new List<TextBlockParam>
            {
                new() { Text = MappingSystemPrompt, CacheControl = new CacheControlEphemeral() }
            },
            Messages = [new() { Role = Role.User, Content = DescribeTable(table) }]
        };

        var response = await client.Messages.Create(parameters);
        var json = response.Content.Select(b => b.Value).OfType<TextBlock>().FirstOrDefault()?.Text
            ?? throw new InvalidOperationException("No text content in mapping response.");

        return ParseProposal(json, table);
    }

    public async Task<RawTable> ExtractTableFromImageAsync(byte[] imageBytes, string mediaType, string fileName, CancellationToken ct = default)
    {
        var client = GetClient();

        var parameters = new MessageCreateParams
        {
            Model = Model.ClaudeOpus4_8,
            MaxTokens = 16000,
            Thinking = new ThinkingConfigAdaptive(),
            OutputConfig = new OutputConfig
            {
                Effort = Effort.High, // messy/handwritten photos benefit from more effort
                Format = new JsonOutputFormat { Schema = ImageSchema() }
            },
            System = new List<TextBlockParam>
            {
                new() { Text = ImageSystemPrompt, CacheControl = new CacheControlEphemeral() }
            },
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = new List<ContentBlockParam>
                    {
                        new ImageBlockParam
                        {
                            Source = new Base64ImageSource
                            {
                                Data = Convert.ToBase64String(imageBytes),
                                MediaType = ToMediaType(mediaType)
                            }
                        },
                        new TextBlockParam { Text = "Extract the table from this photo of a paper record." }
                    }
                }
            ]
        };

        var response = await client.Messages.Create(parameters);
        var json = response.Content.Select(b => b.Value).OfType<TextBlock>().FirstOrDefault()?.Text
            ?? throw new InvalidOperationException("No text content in extraction response.");

        return ParseImageTable(json, fileName);
    }

    private AnthropicClient GetClient()
    {
        var apiKey = _keyProvider.GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Anthropic API key is not configured.");

        lock (_gate)
        {
            if (_client is null || _clientKey != apiKey)
            {
                _client = new AnthropicClient { ApiKey = apiKey };
                _clientKey = apiKey;
            }

            return _client;
        }
    }

    private static MediaType ToMediaType(string mediaType) => mediaType.ToLowerInvariant() switch
    {
        "image/png" => MediaType.ImagePng,
        "image/gif" => MediaType.ImageGif,
        "image/webp" => MediaType.ImageWebP,
        _ => MediaType.ImageJpeg
    };

    // ---- prompt + schema construction ----

    private static readonly string MappingSystemPrompt = BuildSystemPrompt();

    private static string BuildSystemPrompt()
    {
        var sb = new StringBuilder();
        sb.AppendLine("You map messy inventory/sales tables onto a fixed canonical schema for a small-business tracker.");
        sb.AppendLine("Source headers may be in any language (English, Spanish, …) and any layout.");
        sb.AppendLine();
        sb.AppendLine("Canonical fields:");
        foreach (var f in CanonicalSchema.Fields)
            sb.AppendLine($"- {f.Field}{(f.Required ? " (required)" : "")}: {f.Description}");
        sb.AppendLine();
        sb.AppendLine("For each canonical field, choose the single best matching source column by its EXACT name, or \"\" if absent.");
        sb.AppendLine("Use the sample values, not just header text, to decide. Set confidence 0..1.");
        sb.AppendLine("recordType: Arrivals (all incoming), Sales (all outgoing), StockCount (absolute on-hand counts),");
        sb.AppendLine("Mixed (a Direction column decides per row), or Unknown.");
        sb.AppendLine("hints.decimalComma: true if numbers look like 1.234,56. hints.dateFormat: a .NET format like dd/MM/yyyy if clear, else \"\".");
        sb.AppendLine("hints.defaultCurrency: ISO code (USD, EUR, MXN, …) if evident, else \"\".");
        return sb.ToString();
    }

    private static string DescribeTable(RawTable table)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Source file: {table.Meta.FileName}{(table.Meta.SheetName is { } s ? $" (sheet {s})" : "")}");
        if (!string.IsNullOrWhiteSpace(table.Meta.Notes)) sb.AppendLine($"Notes: {table.Meta.Notes}");
        sb.AppendLine();
        sb.AppendLine("Columns (name | inferred type | samples):");
        int i = 1;
        foreach (var c in table.Columns)
            sb.AppendLine($"{i++}. \"{c.Name}\" | {c.InferredType} | {string.Join("; ", c.SampleValues)}");

        sb.AppendLine();
        sb.AppendLine("First rows (JSON):");
        var rows = table.Rows.Take(MaxSampleRows).Select(r => r.Cells);
        sb.AppendLine(JsonSerializer.Serialize(rows));
        return sb.ToString();
    }

    private static Dictionary<string, JsonElement> MappingSchema()
    {
        var canonical = string.Join(",", CanonicalSchema.Fields.Select(f => $"\"{f.Field}\""));
        var recordTypes = string.Join(",", Enum.GetNames<RecordType>().Select(n => $"\"{n}\""));
        var schema = $$"""
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
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(schema)!;
    }

    private static MappingProposal ParseProposal(string json, RawTable table)
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

    private const string ImageSystemPrompt =
        "You transcribe photographed paper records (often handwritten, rotated, or imperfect) into a table.\n" +
        "Return the column headers exactly as written; if there are no headers, invent short ones.\n" +
        "Transcribe each cell's value EXACTLY as it appears — do not reformat numbers, dates, or currency, and do not\n" +
        "compute or infer values. Every row must have one entry per column (use \"\" for blanks). Put any concerns about\n" +
        "legibility in notes.";

    private static Dictionary<string, JsonElement> ImageSchema()
    {
        const string schema = """
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
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(schema)!;
    }

    private static RawTable ParseImageTable(string json, string fileName)
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
