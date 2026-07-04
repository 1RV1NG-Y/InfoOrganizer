namespace InfoOrganizer.Domain;

/// <summary>A tracked item. The canonical columns cover tracking; anything source-specific
/// lives in <see cref="ExtraAttributesJson"/> so no information is lost.</summary>
public class Product
{
    public int Id { get; set; }
    public string? Sku { get; set; }
    public string Name { get; set; } = "";
    public string? Category { get; set; }
    public string? Unit { get; set; }
    public decimal? ReorderThreshold { get; set; }

    /// <summary>JSON object of unmapped, source-specific fields (e.g. {"shelf":"A3","color":"red"}).</summary>
    public string ExtraAttributesJson { get; set; } = "{}";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<StockMovement> Movements { get; set; } = new();
}

/// <summary>A supplier or customer. Kept lightweight; movements may also reference a party by name only.</summary>
public class Party
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public PartyKind Kind { get; set; } = PartyKind.Unknown;
}

/// <summary>One inventory event. <see cref="Quantity"/> is the magnitude for In/Out and a signed
/// delta for Adjustment; <see cref="SignedQuantity"/> exposes the stock-on-hand contribution.</summary>
public class StockMovement
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public MovementKind Kind { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public string? Currency { get; set; }
    public DateOnly? OccurredOn { get; set; }
    public string? LocationName { get; set; }
    public string? PartyName { get; set; }
    public string? Note { get; set; }
    public string ExtraAttributesJson { get; set; } = "{}";

    public int ImportBatchId { get; set; }
    public ImportBatch? ImportBatch { get; set; }

    public int? RawRecordId { get; set; }
    public RawRecord? RawRecord { get; set; }

    public int? ReviewRowId { get; set; }
    public ReviewRow? ReviewRow { get; set; }

    /// <summary>Contribution to stock on hand: +Quantity for In, -Quantity for Out, signed delta for Adjustment.</summary>
    public decimal SignedQuantity => Kind switch
    {
        MovementKind.In => Quantity,
        MovementKind.Out => -Quantity,
        _ => Quantity
    };
}

/// <summary>One upload/ingestion run, with provenance back to the original rows and the mapping used.</summary>
public class ImportBatch
{
    public int Id { get; set; }
    public SourceType SourceType { get; set; }
    public string FileName { get; set; } = "";
    public DateTime ImportedAtUtc { get; set; } = DateTime.UtcNow;

    public int? SourceProfileId { get; set; }
    public SourceProfile? SourceProfile { get; set; }

    public int RowCount { get; set; }
    public ImportStatus Status { get; set; } = ImportStatus.Pending;

    public List<RawRecord> RawRecords { get; set; } = new();
    public List<ReviewRow> ReviewRows { get; set; } = new();
    public List<StockMovement> Movements { get; set; } = new();
}

/// <summary>The original row, captured verbatim as JSON so corrections and audits are always possible.</summary>
public class RawRecord
{
    public int Id { get; set; }
    public int ImportBatchId { get; set; }
    public ImportBatch? ImportBatch { get; set; }
    public int RowIndex { get; set; }

    /// <summary>JSON object of column name -> original cell text.</summary>
    public string DataJson { get; set; } = "{}";
}

/// <summary>A normalized row staged for human review before it is allowed to affect inventory.</summary>
public class ReviewRow
{
    public int Id { get; set; }

    public int ImportBatchId { get; set; }
    public ImportBatch? ImportBatch { get; set; }

    public int? RawRecordId { get; set; }
    public RawRecord? RawRecord { get; set; }

    public int RowIndex { get; set; }
    public ReviewRowStatus Status { get; set; }
    public double Confidence { get; set; }

    public string? ProductName { get; set; }
    public string? Sku { get; set; }
    public string? Category { get; set; }
    public string? Unit { get; set; }
    public MovementKind Kind { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public string? Currency { get; set; }
    public DateOnly? OccurredOn { get; set; }
    public string? LocationName { get; set; }
    public string? PartyName { get; set; }
    public string? Note { get; set; }
    public bool IsAbsoluteCount { get; set; }

    public string IssuesJson { get; set; } = "[]";
    public string ExtraAttributesJson { get; set; } = "{}";

    public StockMovement? Movement { get; set; }
}

/// <summary>A confirmed column→field mapping for one source format, keyed by a column fingerprint.
/// Lets re-uploads of the same format import automatically with no AI call.</summary>
public class SourceProfile
{
    public int Id { get; set; }
    public string Fingerprint { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>Serialized List&lt;FieldMapping&gt;.</summary>
    public string MappingJson { get; set; } = "[]";

    public RecordType DefaultRecordType { get; set; } = RecordType.Unknown;

    /// <summary>Serialized <see cref="MappingHints"/> (date format, decimal style, default currency).</summary>
    public string? HintsJson { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
