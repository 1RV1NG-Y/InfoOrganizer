using InfoOrganizer.Domain;

namespace InfoOrganizer.Application;

/// <summary>A row after the confirmed mapping has been applied and values parsed. Carries any
/// per-row problems so the user can see (and fix) what didn't import cleanly.</summary>
public sealed class NormalizedRow
{
    public int RowIndex { get; init; }
    public string? ProductName { get; set; }
    public string? Sku { get; set; }
    public string? Category { get; set; }
    public string? Unit { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public string? Currency { get; set; }
    public DateOnly? Date { get; set; }
    public string? LocationName { get; set; }
    public string? PartyName { get; set; }
    public string? Note { get; set; }

    public MovementKind Kind { get; set; }
    /// <summary>When true, <see cref="Quantity"/> is an absolute on-hand count; the importer converts
    /// it to an adjustment delta against current stock.</summary>
    public bool IsAbsoluteCount { get; set; }

    /// <summary>Unmapped source columns, preserved on the product/raw record so nothing is lost.</summary>
    public Dictionary<string, string?> Extra { get; set; } = new();

    public List<string> Issues { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public bool IsValid => Issues.Count == 0;
}

/// <summary>Everything the review screen needs: the extracted table and the resolved mapping.</summary>
public sealed class ImportPreview
{
    public required SourceType SourceType { get; init; }
    public required string FileName { get; init; }
    public required RawTable Table { get; init; }
    public required MappingResolution Resolution { get; init; }
}

public sealed record RowIssue(int RowIndex, string Message);

public sealed class ImportResult
{
    public int BatchId { get; init; }
    public int ImportedRows { get; init; }
    public int SkippedRows { get; init; }
    public List<RowIssue> Issues { get; init; } = new();
    public int? SavedProfileId { get; init; }
}

public sealed class StagedImportResult
{
    public int BatchId { get; init; }
    public int RowCount { get; init; }
    public int ReadyRows { get; init; }
    public int NeedsReviewRows { get; init; }
    public int RejectedRows { get; init; }
    public int? SavedProfileId { get; init; }
}

public sealed record ReviewBatchDetails(
    int BatchId,
    string FileName,
    SourceType SourceType,
    ImportStatus Status,
    int RowCount,
    DateTime ImportedAtUtc,
    IReadOnlyList<ReviewRowDetails> Rows);

public sealed record ReviewRowDetails(ReviewRow Row, string RawDataJson);

public sealed record RawRecordDetails(int Id, int RowIndex, string DataJson);

public sealed record ImportMovementDetails(
    int Id,
    string ProductName,
    MovementKind Kind,
    decimal Quantity,
    decimal? UnitPrice,
    string? Currency,
    DateOnly? OccurredOn,
    string? LocationName,
    int? SourceRowIndex);

public sealed record ImportBatchDetails(
    int BatchId,
    string FileName,
    SourceType SourceType,
    ImportStatus Status,
    int RowCount,
    DateTime ImportedAtUtc,
    string? SourceProfileName,
    string? SourceProfileFingerprint,
    int ReadyRows,
    int NeedsReviewRows,
    int AppliedRows,
    int RejectedRows,
    IReadOnlyList<ReviewRowDetails> ReviewRows,
    IReadOnlyList<RawRecordDetails> RawRecords,
    IReadOnlyList<ImportMovementDetails> Movements);

public sealed record ReviewRowsActionResult(
    int RequestedRows,
    int ApprovedRows,
    int RejectedRows,
    int FailedRows,
    int SkippedRows);

public sealed record ImportBatchListItem(
    int BatchId,
    string FileName,
    SourceType SourceType,
    ImportStatus Status,
    int RowCount,
    DateTime ImportedAtUtc,
    int ReadyRows,
    int NeedsReviewRows,
    int AppliedRows,
    int RejectedRows);

public sealed class ReviewRowEdit
{
    public int Id { get; set; }
    public ReviewRowStatus Status { get; set; }
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
}
