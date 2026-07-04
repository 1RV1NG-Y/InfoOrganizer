using System.Text.Json;
using InfoOrganizer.Data;
using InfoOrganizer.Domain;
using InfoOrganizer.Ingestion;
using InfoOrganizer.Mapping;
using Microsoft.EntityFrameworkCore;

namespace InfoOrganizer.Application;

/// <summary>Drives the ingest, mapping, staging, review, and inventory-apply flow.</summary>
public sealed class ImportService
{
    private readonly IEnumerable<ISourceAdapter> _adapters;
    private readonly IColumnProfiler _profiler;
    private readonly IMappingEngine _mappingEngine;
    private readonly INormalizer _normalizer;
    private readonly IRowConfidenceScorer _rowConfidenceScorer;
    private readonly ISourceProfileStore _profileStore;
    private readonly IDbContextFactory<AppDbContext> _factory;

    public ImportService(
        IEnumerable<ISourceAdapter> adapters,
        IColumnProfiler profiler,
        IMappingEngine mappingEngine,
        INormalizer normalizer,
        IRowConfidenceScorer rowConfidenceScorer,
        ISourceProfileStore profileStore,
        IDbContextFactory<AppDbContext> factory)
    {
        _adapters = adapters;
        _profiler = profiler;
        _mappingEngine = mappingEngine;
        _normalizer = normalizer;
        _rowConfidenceScorer = rowConfidenceScorer;
        _profileStore = profileStore;
        _factory = factory;
    }

    public async Task<IReadOnlyList<ImportPreview>> PrepareAsync(UploadedFile file, CancellationToken ct = default)
    {
        var adapter = _adapters.FirstOrDefault(a => a.CanHandle(file))
            ?? throw new NotSupportedException($"No importer handles \"{file.FileName}\".");

        var tables = await adapter.ExtractAsync(file, ct);
        var previews = new List<ImportPreview>();
        foreach (var table in tables)
        {
            _profiler.Profile(table);
            var resolution = await _mappingEngine.ResolveAsync(table, ct);
            previews.Add(new ImportPreview
            {
                SourceType = table.Meta.SourceType,
                FileName = file.FileName,
                Table = table,
                Resolution = resolution
            });
        }

        return previews;
    }

    public async Task<StagedImportResult> StageAsync(
        ImportPreview preview,
        MappingProposal confirmed,
        string profileName,
        bool saveProfile,
        CancellationToken ct = default)
    {
        var normalized = _normalizer.Normalize(preview.Table, confirmed);

        await using var db = await _factory.CreateDbContextAsync(ct);

        var batch = new ImportBatch
        {
            SourceType = preview.SourceType,
            FileName = preview.FileName,
            Status = ImportStatus.AwaitingReview,
            RowCount = preview.Table.Rows.Count
        };
        db.ImportBatches.Add(batch);

        int ready = 0, needsReview = 0;
        for (int i = 0; i < normalized.Count; i++)
        {
            var row = normalized[i];

            var rawRecord = new RawRecord
            {
                RowIndex = row.RowIndex,
                DataJson = JsonSerializer.Serialize(preview.Table.Rows[i].Cells)
            };
            batch.RawRecords.Add(rawRecord);

            var review = ToReviewRow(row, rawRecord, confirmed, preview.SourceType);
            batch.ReviewRows.Add(review);

            if (review.Status == ReviewRowStatus.Ready) ready++;
            else if (review.Status == ReviewRowStatus.NeedsReview) needsReview++;
        }

        int? savedProfileId = null;
        if (saveProfile && !string.IsNullOrWhiteSpace(preview.Resolution.Fingerprint))
        {
            var profile = await _profileStore.SaveAsync(new SourceProfile
            {
                Fingerprint = preview.Resolution.Fingerprint,
                Name = string.IsNullOrWhiteSpace(profileName) ? preview.FileName : profileName,
                MappingJson = MappingSerializer.SerializeFields(confirmed.Fields),
                DefaultRecordType = confirmed.DetectedRecordType,
                HintsJson = MappingSerializer.SerializeHints(confirmed.Hints)
            }, ct);
            savedProfileId = profile.Id;
            batch.SourceProfileId = profile.Id;
        }

        await db.SaveChangesAsync(ct);

        return new StagedImportResult
        {
            BatchId = batch.Id,
            RowCount = batch.RowCount,
            ReadyRows = ready,
            NeedsReviewRows = needsReview,
            SavedProfileId = savedProfileId
        };
    }

    /// <summary>Compatibility helper for tests and simple callers: stage, then apply all ready rows.</summary>
    public async Task<ImportResult> CommitAsync(
        ImportPreview preview,
        MappingProposal confirmed,
        string profileName,
        bool saveProfile,
        CancellationToken ct = default)
    {
        var staged = await StageAsync(preview, confirmed, profileName, saveProfile, ct);
        var applied = await ApplyReadyRowsAsync(staged.BatchId, ct);

        return new ImportResult
        {
            BatchId = staged.BatchId,
            ImportedRows = applied.ImportedRows,
            SkippedRows = staged.NeedsReviewRows + staged.RejectedRows,
            Issues = applied.Issues,
            SavedProfileId = staged.SavedProfileId
        };
    }

    public async Task<ReviewBatchDetails?> GetReviewBatchAsync(int batchId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var batch = await db.ImportBatches.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == batchId, ct);

        if (batch is null) return null;

        var rows = await GetReviewRowDetailsAsync(db, batchId, ct);

        return new ReviewBatchDetails(
            batch.Id,
            batch.FileName,
            batch.SourceType,
            batch.Status,
            batch.RowCount,
            batch.ImportedAtUtc,
            rows);
    }

    public async Task<ImportBatchDetails?> GetImportBatchDetailsAsync(int batchId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var batch = await db.ImportBatches.AsNoTracking()
            .Where(b => b.Id == batchId)
            .Select(b => new
            {
                b.Id,
                b.FileName,
                b.SourceType,
                b.Status,
                b.RowCount,
                b.ImportedAtUtc,
                SourceProfileName = b.SourceProfile != null ? b.SourceProfile.Name : null,
                SourceProfileFingerprint = b.SourceProfile != null ? b.SourceProfile.Fingerprint : null
            })
            .FirstOrDefaultAsync(ct);

        if (batch is null) return null;

        var reviewRows = await GetReviewRowDetailsAsync(db, batchId, ct);
        var rawRecords = await db.RawRecords.AsNoTracking()
            .Where(r => r.ImportBatchId == batchId)
            .OrderBy(r => r.RowIndex)
            .Select(r => new RawRecordDetails(r.Id, r.RowIndex, r.DataJson))
            .ToListAsync(ct);
        var movements = await db.Movements.AsNoTracking()
            .Where(m => m.ImportBatchId == batchId)
            .OrderBy(m => m.Id)
            .Select(m => new ImportMovementDetails(
                m.Id,
                m.Product != null ? m.Product.Name : "",
                m.Kind,
                m.Quantity,
                m.UnitPrice,
                m.Currency,
                m.OccurredOn,
                m.LocationName,
                m.ReviewRow != null ? m.ReviewRow.RowIndex : m.RawRecord != null ? m.RawRecord.RowIndex : null))
            .ToListAsync(ct);

        return new ImportBatchDetails(
            batch.Id,
            batch.FileName,
            batch.SourceType,
            batch.Status,
            batch.RowCount,
            batch.ImportedAtUtc,
            batch.SourceProfileName,
            batch.SourceProfileFingerprint,
            reviewRows.Count(r => r.Row.Status == ReviewRowStatus.Ready),
            reviewRows.Count(r => r.Row.Status == ReviewRowStatus.NeedsReview),
            reviewRows.Count(r => r.Row.Status == ReviewRowStatus.Applied),
            reviewRows.Count(r => r.Row.Status == ReviewRowStatus.Rejected),
            reviewRows,
            rawRecords,
            movements);
    }

    public async Task<IReadOnlyList<ImportBatchListItem>> GetImportBatchesAsync(int take = 100, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var batches = await db.ImportBatches.AsNoTracking()
            .Include(b => b.ReviewRows)
            .OrderByDescending(b => b.Id)
            .Take(take)
            .ToListAsync(ct);

        return batches.Select(b => new ImportBatchListItem(
                b.Id,
                b.FileName,
                b.SourceType,
                b.Status,
                b.RowCount,
                b.ImportedAtUtc,
                b.ReviewRows.Count(r => r.Status == ReviewRowStatus.Ready),
                b.ReviewRows.Count(r => r.Status == ReviewRowStatus.NeedsReview),
                b.ReviewRows.Count(r => r.Status == ReviewRowStatus.Applied),
                b.ReviewRows.Count(r => r.Status == ReviewRowStatus.Rejected)))
            .ToList();
    }

    public async Task UpdateReviewRowAsync(ReviewRowEdit edit, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var row = await db.ReviewRows.FirstOrDefaultAsync(r => r.Id == edit.Id, ct)
            ?? throw new InvalidOperationException("Review row was not found.");

        if (row.Status == ReviewRowStatus.Applied)
            throw new InvalidOperationException("Applied rows cannot be edited.");

        row.ProductName = Clean(edit.ProductName);
        row.Sku = Clean(edit.Sku);
        row.Category = Clean(edit.Category);
        row.Unit = Clean(edit.Unit);
        row.Kind = edit.Kind;
        row.Quantity = edit.Kind == MovementKind.Adjustment ? edit.Quantity : Math.Abs(edit.Quantity);
        row.UnitPrice = edit.UnitPrice;
        row.Currency = Clean(edit.Currency)?.ToUpperInvariant();
        row.OccurredOn = edit.OccurredOn;
        row.LocationName = Clean(edit.LocationName);
        row.PartyName = Clean(edit.PartyName);
        row.Note = Clean(edit.Note);

        if (edit.Status == ReviewRowStatus.Rejected)
        {
            row.Status = ReviewRowStatus.Rejected;
            row.IssuesJson = "[]";
        }
        else if (edit.Status == ReviewRowStatus.Ready)
        {
            MarkReadyIfValid(row);
        }
        else
        {
            SetValidationIssues(row);
            row.Status = ReviewRowStatus.NeedsReview;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task RejectReviewRowAsync(int rowId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var row = await db.ReviewRows.FirstOrDefaultAsync(r => r.Id == rowId, ct)
            ?? throw new InvalidOperationException("Review row was not found.");

        if (row.Status == ReviewRowStatus.Applied)
            throw new InvalidOperationException("Applied rows cannot be rejected.");

        row.Status = ReviewRowStatus.Rejected;
        row.IssuesJson = "[]";
        await db.SaveChangesAsync(ct);
    }

    public async Task<ReviewRowsActionResult> ApproveReviewRowsAsync(IEnumerable<int> ids, CancellationToken ct = default)
    {
        var distinctIds = ids.Distinct().ToList();
        if (distinctIds.Count == 0)
            return new ReviewRowsActionResult(0, 0, 0, 0, 0);

        await using var db = await _factory.CreateDbContextAsync(ct);
        var rows = await db.ReviewRows
            .Where(r => distinctIds.Contains(r.Id) && r.Status != ReviewRowStatus.Applied)
            .ToListAsync(ct);

        var approved = 0;
        var failed = 0;
        foreach (var row in rows)
        {
            if (MarkReadyIfValid(row))
                approved++;
            else
                failed++;
        }

        await db.SaveChangesAsync(ct);
        return new ReviewRowsActionResult(distinctIds.Count, approved, 0, failed, distinctIds.Count - rows.Count);
    }

    public async Task<ReviewRowsActionResult> RejectReviewRowsAsync(IEnumerable<int> ids, CancellationToken ct = default)
    {
        var distinctIds = ids.Distinct().ToList();
        if (distinctIds.Count == 0)
            return new ReviewRowsActionResult(0, 0, 0, 0, 0);

        await using var db = await _factory.CreateDbContextAsync(ct);
        var rows = await db.ReviewRows
            .Where(r => distinctIds.Contains(r.Id) && r.Status != ReviewRowStatus.Applied)
            .ToListAsync(ct);

        foreach (var row in rows)
        {
            row.Status = ReviewRowStatus.Rejected;
            row.IssuesJson = "[]";
        }

        await db.SaveChangesAsync(ct);
        return new ReviewRowsActionResult(distinctIds.Count, 0, rows.Count, 0, distinctIds.Count - rows.Count);
    }

    public async Task<ImportResult> ApplyReadyRowsAsync(int batchId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var batch = await db.ImportBatches
            .Include(b => b.ReviewRows.Where(r => r.Status == ReviewRowStatus.Ready))
            .FirstOrDefaultAsync(b => b.Id == batchId, ct)
            ?? throw new InvalidOperationException("Import batch was not found.");

        var products = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase);
        var onHand = new Dictionary<Product, decimal>();
        var issues = new List<RowIssue>();
        var imported = 0;

        foreach (var row in batch.ReviewRows.OrderBy(r => r.RowIndex))
        {
            var validation = Validate(row);
            if (validation.Count > 0)
            {
                row.Status = ReviewRowStatus.NeedsReview;
                row.IssuesJson = JsonSerializer.Serialize(validation);
                foreach (var issue in validation) issues.Add(new RowIssue(row.RowIndex, issue));
                continue;
            }

            var product = await UpsertProductAsync(db, products, onHand, row, ct);

            MovementKind kind;
            decimal quantity;
            if (row.IsAbsoluteCount)
            {
                kind = MovementKind.Adjustment;
                quantity = row.Quantity - onHand[product];
                onHand[product] = row.Quantity;
            }
            else
            {
                kind = row.Kind;
                quantity = row.Quantity;
                onHand[product] += Signed(kind, quantity);
            }

            db.Movements.Add(new StockMovement
            {
                Product = product,
                Kind = kind,
                Quantity = quantity,
                UnitPrice = row.UnitPrice,
                Currency = row.Currency,
                OccurredOn = row.OccurredOn,
                LocationName = row.LocationName,
                PartyName = row.PartyName,
                Note = row.Note,
                ExtraAttributesJson = row.ExtraAttributesJson,
                RawRecordId = row.RawRecordId,
                ReviewRow = row,
                ImportBatch = batch
            });

            row.Status = ReviewRowStatus.Applied;
            row.IssuesJson = "[]";
            imported++;
        }

        await db.SaveChangesAsync(ct);
        await RefreshBatchStatusAsync(db, batchId, ct);
        await db.SaveChangesAsync(ct);

        var skipped = await db.ReviewRows
            .Where(r => r.ImportBatchId == batchId && r.Status != ReviewRowStatus.Applied)
            .CountAsync(ct);

        return new ImportResult
        {
            BatchId = batchId,
            ImportedRows = imported,
            SkippedRows = skipped,
            Issues = issues
        };
    }

    private ReviewRow ToReviewRow(NormalizedRow row, RawRecord rawRecord, MappingProposal mapping, SourceType sourceType)
    {
        var score = _rowConfidenceScorer.Score(row, mapping, sourceType);
        return new ReviewRow
        {
            RowIndex = row.RowIndex,
            RawRecord = rawRecord,
            Status = score.SuggestedStatus,
            Confidence = score.Confidence,
            ProductName = row.ProductName,
            Sku = row.Sku,
            Category = row.Category,
            Unit = row.Unit,
            Kind = row.Kind,
            Quantity = row.Quantity,
            UnitPrice = row.UnitPrice,
            Currency = row.Currency,
            OccurredOn = row.Date,
            LocationName = row.LocationName,
            PartyName = row.PartyName,
            Note = row.Note,
            IsAbsoluteCount = row.IsAbsoluteCount,
            IssuesJson = JsonSerializer.Serialize(row.Issues),
            ExtraAttributesJson = JsonSerializer.Serialize(row.Extra)
        };
    }

    private static async Task<IReadOnlyList<ReviewRowDetails>> GetReviewRowDetailsAsync(
        AppDbContext db,
        int batchId,
        CancellationToken ct)
    {
        var rows = await db.ReviewRows.AsNoTracking()
            .Where(r => r.ImportBatchId == batchId)
            .OrderBy(r => r.RowIndex)
            .Select(r => new
            {
                Row = r,
                RawDataJson = r.RawRecord != null ? r.RawRecord.DataJson : "{}"
            })
            .ToListAsync(ct);

        return rows.Select(r => new ReviewRowDetails(r.Row, r.RawDataJson)).ToList();
    }

    private static async Task<Product> UpsertProductAsync(
        AppDbContext db,
        Dictionary<string, Product> cache,
        Dictionary<Product, decimal> onHand,
        ReviewRow row,
        CancellationToken ct)
    {
        var key = row.Sku is not null ? $"sku:{row.Sku.ToLowerInvariant()}" : $"name:{row.ProductName?.ToLowerInvariant()}";
        if (cache.TryGetValue(key, out var cached)) return cached;

        Product? product = null;
        if (row.Sku is not null)
            product = await db.Products.FirstOrDefaultAsync(p => p.Sku == row.Sku, ct);
        if (product is null && row.ProductName is not null)
            product = await db.Products.FirstOrDefaultAsync(p => p.Name == row.ProductName, ct);

        if (product is null)
        {
            product = new Product
            {
                Sku = row.Sku,
                Name = row.ProductName ?? row.Sku ?? "(unnamed)",
                Category = row.Category,
                Unit = row.Unit,
                ExtraAttributesJson = "{}"
            };
            db.Products.Add(product);
        }
        else
        {
            product.Category ??= row.Category;
            product.Unit ??= row.Unit;
            if (product.Sku is null && row.Sku is not null) product.Sku = row.Sku;
        }

        cache[key] = product;

        if (!onHand.ContainsKey(product))
        {
            decimal current = 0;
            if (product.Id != 0)
            {
                var existing = await db.Movements
                    .Where(m => m.ProductId == product.Id)
                    .Select(m => new { m.Kind, m.Quantity })
                    .ToListAsync(ct);
                current = existing.Sum(m => Signed(m.Kind, m.Quantity));
            }
            onHand[product] = current;
        }

        return product;
    }

    private static async Task RefreshBatchStatusAsync(AppDbContext db, int batchId, CancellationToken ct)
    {
        var batch = await db.ImportBatches.FirstAsync(b => b.Id == batchId, ct);
        var hasOpenRows = await db.ReviewRows.AnyAsync(r =>
            r.ImportBatchId == batchId &&
            (r.Status == ReviewRowStatus.Ready || r.Status == ReviewRowStatus.NeedsReview), ct);

        batch.Status = hasOpenRows ? ImportStatus.AwaitingReview : ImportStatus.Imported;
    }

    private static bool MarkReadyIfValid(ReviewRow row)
    {
        var issues = SetValidationIssues(row);
        if (issues.Count == 0)
        {
            row.Status = ReviewRowStatus.Ready;
            row.Confidence = 1.0;
            return true;
        }

        row.Status = ReviewRowStatus.NeedsReview;
        return false;
    }

    private static List<string> SetValidationIssues(ReviewRow row)
    {
        var issues = Validate(row);
        row.IssuesJson = JsonSerializer.Serialize(issues);
        return issues;
    }

    private static List<string> Validate(ReviewRow row)
    {
        var issues = new List<string>();
        if (string.IsNullOrWhiteSpace(row.ProductName) && string.IsNullOrWhiteSpace(row.Sku))
            issues.Add("No product name or code.");
        if (row.Quantity == 0 && !row.IsAbsoluteCount)
            issues.Add("Quantity is zero.");
        return issues;
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static decimal Signed(MovementKind kind, decimal quantity) => kind switch
    {
        MovementKind.In => quantity,
        MovementKind.Out => -quantity,
        _ => quantity
    };
}
