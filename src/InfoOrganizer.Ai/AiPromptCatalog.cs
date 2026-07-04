using System.Text;
using System.Text.Json;
using InfoOrganizer.Domain;

namespace InfoOrganizer.Ai;

internal static class AiPromptCatalog
{
    private const int MaxSampleRows = 8;

    public static readonly string MappingSystemPrompt = BuildSystemPrompt();

    public const string ImageSystemPrompt =
        "You transcribe photographed paper records (often handwritten, rotated, or imperfect) into a table.\n" +
        "Return the column headers exactly as written; if there are no headers, invent short ones.\n" +
        "Transcribe each cell's value EXACTLY as it appears — do not reformat numbers, dates, or currency, and do not\n" +
        "compute or infer values. Every row must have one entry per column (use \"\" for blanks). Put any concerns about\n" +
        "legibility in notes.";

    public const string ImageUserPrompt = "Extract the table from this photo of a paper record.";

    public static string DescribeTable(RawTable table)
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
}
