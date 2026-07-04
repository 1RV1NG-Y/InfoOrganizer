namespace InfoOrganizer.Domain;

/// <summary>Direction of a stock movement. Sign for stock-on-hand is derived from this.</summary>
public enum MovementKind
{
    /// <summary>Inventory arriving (purchase, restock). Adds to stock.</summary>
    In,
    /// <summary>Inventory leaving (sale, shrinkage). Removes from stock.</summary>
    Out,
    /// <summary>Manual correction. Quantity is a signed delta against current stock.</summary>
    Adjustment
}

/// <summary>What an imported table represents as a whole.</summary>
public enum RecordType
{
    Unknown,
    /// <summary>Every row is incoming inventory.</summary>
    Arrivals,
    /// <summary>Every row is a sale / outgoing.</summary>
    Sales,
    /// <summary>Rows are absolute on-hand counts (become Adjustments).</summary>
    StockCount,
    /// <summary>Direction varies per row; a Direction column decides In vs Out.</summary>
    Mixed
}

public enum SourceType
{
    Excel = 0,
    Image = 1,
    Csv = 2
}

public enum ImportStatus
{
    /// <summary>File received, not yet extracted.</summary>
    Pending,
    /// <summary>Extracted and mapped; waiting on the user to confirm.</summary>
    AwaitingReview,
    /// <summary>Rows committed to the canonical tables.</summary>
    Imported,
    Failed
}

public enum ReviewRowStatus
{
    NeedsReview,
    Ready,
    Applied,
    Rejected
}

public enum PartyKind
{
    Unknown,
    Supplier,
    Customer
}

/// <summary>The fixed set of fields the system tracks. Any source column maps onto these (or onto nothing).</summary>
public enum CanonicalField
{
    ProductName,
    Sku,
    Category,
    Unit,
    Quantity,
    UnitPrice,
    Currency,
    Date,
    /// <summary>Warehouse, store, branch, or other place where the movement happened.</summary>
    Location,
    PartyName,
    /// <summary>A column whose value indicates In vs Out (used when RecordType is Mixed).</summary>
    Direction,
    Note
}

/// <summary>Best-effort type inferred for a raw column during profiling.</summary>
public enum RawCellType
{
    Unknown,
    Text,
    Number,
    Date,
    Boolean,
    Currency
}

/// <summary>Where a resolved mapping came from.</summary>
public enum MappingSource
{
    /// <summary>Reused from a previously confirmed SourceProfile (no AI call).</summary>
    SavedProfile,
    Ai,
    Heuristic
}
