namespace InfoOrganizer.Domain;

/// <summary>The neutral, format-agnostic shape that BOTH Excel and photo inputs are reduced to.
/// Everything downstream (profiling, mapping, normalization) operates on this single type.</summary>
public sealed class RawTable
{
    public List<RawColumn> Columns { get; set; } = new();
    public List<RawRow> Rows { get; set; } = new();
    public SourceMeta Meta { get; set; } = new();

    public IEnumerable<string> ColumnNames => Columns.Select(c => c.Name);
}

public sealed class RawColumn
{
    public string Name { get; set; } = "";
    public RawCellType InferredType { get; set; } = RawCellType.Unknown;

    /// <summary>A few non-empty example values, used for AI mapping context and the review UI.</summary>
    public List<string> SampleValues { get; set; } = new();
}

public sealed class RawRow
{
    public int Index { get; set; }

    /// <summary>Column name → original cell text (null/empty allowed). Typed reading happens at normalization time.</summary>
    public Dictionary<string, string?> Cells { get; set; } = new();

    public string? this[string column] => Cells.TryGetValue(column, out var v) ? v : null;
}

public sealed class SourceMeta
{
    public SourceType SourceType { get; set; }
    public string FileName { get; set; } = "";
    public string? SheetName { get; set; }

    /// <summary>Free-form notes, e.g. the vision model's remarks about a low-quality photo.</summary>
    public string? Notes { get; set; }
}
