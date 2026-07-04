namespace InfoOrganizer.Domain;

/// <summary>Maps one canonical field to a source column (or to nothing).</summary>
public sealed class FieldMapping
{
    public CanonicalField Field { get; set; }

    /// <summary>Source column name, or null when this field is not present in the source.</summary>
    public string? SourceColumn { get; set; }

    /// <summary>0..1 confidence in this mapping (from heuristics or the AI proposer).</summary>
    public double Confidence { get; set; }
}

/// <summary>Locale/format hints captured once so re-imports parse values consistently.</summary>
public sealed class MappingHints
{
    /// <summary>.NET date format, e.g. "dd/MM/yyyy". Null = let the parser try common formats.</summary>
    public string? DateFormat { get; set; }

    /// <summary>True for "1.234,56" style numbers (comma decimal separator).</summary>
    public bool DecimalComma { get; set; }

    public string? DefaultCurrency { get; set; } = "MXN";
}

/// <summary>A proposed interpretation of a source table: which columns map where, what the rows
/// represent, and how to parse values. Produced by the heuristic or AI mapper, confirmed by a human.</summary>
public sealed class MappingProposal
{
    public List<FieldMapping> Fields { get; set; } = new();
    public RecordType DetectedRecordType { get; set; } = RecordType.Unknown;
    public MappingHints Hints { get; set; } = new();
    public double OverallConfidence { get; set; }

    /// <summary>Short human-readable explanation of the mapping decision (for the review UI).</summary>
    public string? Rationale { get; set; }

    public string? Column(CanonicalField field) =>
        Fields.FirstOrDefault(f => f.Field == field)?.SourceColumn;
}

/// <summary>Result of asking the mapping engine to resolve a table: either auto-applied from a saved
/// profile, or a fresh proposal that needs human confirmation.</summary>
public sealed class MappingResolution
{
    public string Fingerprint { get; set; } = "";
    public MappingProposal Proposal { get; set; } = new();
    public bool RequiresConfirmation { get; set; }
    public int? SourceProfileId { get; set; }
    public MappingSource Source { get; set; }
}

public sealed record CanonicalFieldInfo(CanonicalField Field, string Description, bool Required);

/// <summary>Describes the canonical target to humans and to the AI proposer.</summary>
public static class CanonicalSchema
{
    public static IReadOnlyList<CanonicalFieldInfo> Fields { get; } = new[]
    {
        new CanonicalFieldInfo(CanonicalField.ProductName, "Name or description of the item/product", true),
        new CanonicalFieldInfo(CanonicalField.Sku, "Item code, SKU, barcode, or reference number", false),
        new CanonicalFieldInfo(CanonicalField.Category, "Category, type, or group of the item", false),
        new CanonicalFieldInfo(CanonicalField.Unit, "Unit of measure (each, kg, box, litre, …)", false),
        new CanonicalFieldInfo(CanonicalField.Quantity, "How many units moved (numeric)", true),
        new CanonicalFieldInfo(CanonicalField.UnitPrice, "Price or cost per unit (numeric)", false),
        new CanonicalFieldInfo(CanonicalField.Currency, "Currency of the price (USD, EUR, MXN, …)", false),
        new CanonicalFieldInfo(CanonicalField.Date, "Date the movement happened", false),
        new CanonicalFieldInfo(CanonicalField.Location, "Warehouse, store, branch, or location for this movement", false),
        new CanonicalFieldInfo(CanonicalField.PartyName, "Supplier (for arrivals) or customer (for sales)", false),
        new CanonicalFieldInfo(CanonicalField.Direction, "A column indicating in/out, buy/sell, received/sold", false),
        new CanonicalFieldInfo(CanonicalField.Note, "Any remark or comment", false),
    };

    public static CanonicalFieldInfo Info(CanonicalField field) => Fields.First(f => f.Field == field);
}
