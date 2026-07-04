using System.Text;
using System.Text.Json;
using InfoOrganizer.Application;
using InfoOrganizer.Data;
using InfoOrganizer.Domain;
using InfoOrganizer.Ingestion;
using InfoOrganizer.Mapping;
using InfoOrganizer.Tests.Support;

namespace InfoOrganizer.Tests;

public class ImportPersistenceTests
{
    private static ImportService BuildService(TestDb db)
    {
        var store = new SourceProfileStore(db);
        var engine = new MappingEngine(store, new HeuristicMapper(), new FakeAiClient { IsConfigured = false });
        return new ImportService(
            new ISourceAdapter[] { new ExcelSourceAdapter(), new CsvSourceAdapter() },
            new ColumnProfiler(),
            engine,
            new Normalizer(),
            new RowConfidenceScorer(),
            store,
            db);
    }

    [Fact]
    public async Task Imports_arrivals_computes_stock_and_reuses_saved_profile()
    {
        using var db = new TestDb();
        var service = BuildService(db);

        var preview = (await service.PrepareAsync(
            new UploadedFile { FileName = "compras-marzo.xlsx", Content = Workbooks.ArrivalsEs() })).Single();

        Assert.True(preview.Resolution.RequiresConfirmation);
        Assert.Equal(RecordType.Arrivals, preview.Resolution.Proposal.DetectedRecordType);

        var result = await service.CommitAsync(preview, preview.Resolution.Proposal, "Compras ES", saveProfile: true);
        Assert.Equal(3, result.ImportedRows);
        Assert.Equal(0, result.SkippedRows);

        var levels = await new TrackingService(db).GetStockLevelsAsync();
        Assert.Equal(100m, levels.Single(l => l.Name == "Manzana").OnHand);
        Assert.Equal(40m, levels.Single(l => l.Name == "Pera").OnHand);
        Assert.Equal(12m, levels.Single(l => l.Name == "Platano").OnHand);

        // Same column shape reappears → mapping auto-reused, no confirmation needed.
        var second = (await service.PrepareAsync(
            new UploadedFile { FileName = "compras-abril.xlsx", Content = Workbooks.ArrivalsEs() })).Single();
        Assert.False(second.Resolution.RequiresConfirmation);
        Assert.Equal(MappingSource.SavedProfile, second.Resolution.Source);
    }

    [Fact]
    public async Task Stock_count_creates_adjustment_to_absolute_quantity()
    {
        using var db = new TestDb();
        var service = BuildService(db);

        var arrivals = (await service.PrepareAsync(
            new UploadedFile { FileName = "compras.xlsx", Content = Workbooks.ArrivalsEs() })).Single();
        await service.CommitAsync(arrivals, arrivals.Resolution.Proposal, "Compras", saveProfile: false);

        var count = (await service.PrepareAsync(
            new UploadedFile { FileName = "inventario-conteo.xlsx", Content = Workbooks.StockCountEs("Manzana", 70) })).Single();
        Assert.Equal(RecordType.StockCount, count.Resolution.Proposal.DetectedRecordType);
        await service.CommitAsync(count, count.Resolution.Proposal, "Conteo", saveProfile: false);

        var manzana = (await new TrackingService(db).GetStockLevelsAsync()).Single(l => l.Name == "Manzana");
        Assert.Equal(70m, manzana.OnHand); // 100 arrived, counted 70 → adjustment of -30
    }

    [Fact]
    public async Task Stage_does_not_change_inventory_until_ready_rows_are_applied()
    {
        using var db = new TestDb();
        var service = BuildService(db);

        var preview = (await service.PrepareAsync(
            new UploadedFile { FileName = "compras.xlsx", Content = Workbooks.ArrivalsEs() })).Single();

        var staged = await service.StageAsync(preview, preview.Resolution.Proposal, "Compras", saveProfile: false);

        using (var ctx = db.CreateDbContext())
        {
            Assert.Equal(3, ctx.RawRecords.Count());
            Assert.Equal(3, ctx.ReviewRows.Count());
            Assert.Equal(3, ctx.ReviewRows.Count(r => r.Status == ReviewRowStatus.Ready));
            Assert.Equal(0, ctx.Movements.Count());
            Assert.Equal(0, ctx.Products.Count());
        }

        var applied = await service.ApplyReadyRowsAsync(staged.BatchId);
        Assert.Equal(3, applied.ImportedRows);

        using (var ctx = db.CreateDbContext())
        {
            Assert.Equal(3, ctx.Movements.Count());
            Assert.Equal(3, ctx.ReviewRows.Count(r => r.Status == ReviewRowStatus.Applied));
        }
    }

    [Fact]
    public async Task Rejected_review_rows_do_not_create_stock_movements()
    {
        using var db = new TestDb();
        var service = BuildService(db);

        var preview = (await service.PrepareAsync(
            new UploadedFile { FileName = "compras.xlsx", Content = Workbooks.ArrivalsEs() })).Single();
        var staged = await service.StageAsync(preview, preview.Resolution.Proposal, "Compras", saveProfile: false);

        int rejectedId;
        using (var ctx = db.CreateDbContext())
        {
            rejectedId = ctx.ReviewRows.Single(r => r.ProductName == "Manzana").Id;
        }

        await service.RejectReviewRowAsync(rejectedId);
        var applied = await service.ApplyReadyRowsAsync(staged.BatchId);

        Assert.Equal(2, applied.ImportedRows);
        using (var ctx = db.CreateDbContext())
        {
            Assert.Equal(2, ctx.Movements.Count());
            Assert.Equal(1, ctx.ReviewRows.Count(r => r.Status == ReviewRowStatus.Rejected));
            Assert.Equal(2, ctx.ReviewRows.Count(r => r.Status == ReviewRowStatus.Applied));
            Assert.DoesNotContain(ctx.Products, p => p.Name == "Manzana");
        }
    }

    [Fact]
    public async Task Bulk_approve_validates_current_values_and_reports_failures()
    {
        using var db = new TestDb();
        var service = BuildService(db);

        var reviewPreview = ReviewGatePreview();
        var staged = await service.StageAsync(reviewPreview, reviewPreview.Resolution.Proposal, "Photo", saveProfile: false);
        Assert.Equal(0, staged.ReadyRows);
        Assert.Equal(2, staged.NeedsReviewRows);

        int[] rowIds;
        using (var ctx = db.CreateDbContext())
        {
            rowIds = ctx.ReviewRows.OrderBy(r => r.RowIndex).Select(r => r.Id).ToArray();
        }

        var result = await service.ApproveReviewRowsAsync(rowIds);

        Assert.Equal(2, result.RequestedRows);
        Assert.Equal(1, result.ApprovedRows);
        Assert.Equal(1, result.FailedRows);

        using (var ctx = db.CreateDbContext())
        {
            var valid = ctx.ReviewRows.Single(r => r.ProductName == "Widget");
            var invalid = ctx.ReviewRows.Single(r => r.ProductName == "Bolt");
            Assert.Equal(ReviewRowStatus.Ready, valid.Status);
            Assert.Equal(1.0, valid.Confidence);
            Assert.Equal(ReviewRowStatus.NeedsReview, invalid.Status);
            Assert.NotEmpty(JsonSerializer.Deserialize<List<string>>(invalid.IssuesJson)!);
        }
    }

    [Fact]
    public async Task Bulk_rejected_review_rows_do_not_create_stock_movements()
    {
        using var db = new TestDb();
        var service = BuildService(db);

        var preview = (await service.PrepareAsync(
            new UploadedFile { FileName = "compras.xlsx", Content = Workbooks.ArrivalsEs() })).Single();
        var staged = await service.StageAsync(preview, preview.Resolution.Proposal, "Compras", saveProfile: false);

        int[] rejectedIds;
        using (var ctx = db.CreateDbContext())
        {
            rejectedIds = ctx.ReviewRows
                .Where(r => r.ProductName == "Manzana" || r.ProductName == "Pera")
                .Select(r => r.Id)
                .ToArray();
        }

        var rejected = await service.RejectReviewRowsAsync(rejectedIds);
        Assert.Equal(2, rejected.RejectedRows);

        var applied = await service.ApplyReadyRowsAsync(staged.BatchId);
        Assert.Equal(1, applied.ImportedRows);

        using (var ctx = db.CreateDbContext())
        {
            Assert.Equal(1, ctx.Movements.Count());
            Assert.Equal(2, ctx.ReviewRows.Count(r => r.Status == ReviewRowStatus.Rejected));
            Assert.Equal(1, ctx.ReviewRows.Count(r => r.Status == ReviewRowStatus.Applied));
            Assert.DoesNotContain(ctx.Products, p => p.Name == "Manzana");
            Assert.DoesNotContain(ctx.Products, p => p.Name == "Pera");
            Assert.Contains(ctx.Products, p => p.Name == "Platano");
        }
    }

    [Fact]
    public async Task Batch_detail_query_returns_provenance_rows_movements_and_profile()
    {
        using var db = new TestDb();
        var service = BuildService(db);

        var preview = (await service.PrepareAsync(
            new UploadedFile { FileName = "compras.xlsx", Content = Workbooks.ArrivalsEs() })).Single();
        var staged = await service.StageAsync(preview, preview.Resolution.Proposal, "Compras detail", saveProfile: true);
        await service.ApplyReadyRowsAsync(staged.BatchId);

        var detail = await service.GetImportBatchDetailsAsync(staged.BatchId);

        Assert.NotNull(detail);
        Assert.Equal("Compras detail", detail.SourceProfileName);
        Assert.False(string.IsNullOrWhiteSpace(detail.SourceProfileFingerprint));
        Assert.Equal(3, detail.RawRecords.Count);
        Assert.Equal(3, detail.ReviewRows.Count);
        Assert.Equal(3, detail.Movements.Count);
        Assert.Equal(3, detail.AppliedRows);
        Assert.All(detail.RawRecords, r => Assert.Contains("Producto", r.DataJson));
        Assert.Equal(new[] { 0, 1, 2 }, detail.RawRecords.Select(r => r.RowIndex));
        Assert.Equal(new[] { 0, 1, 2 }, detail.ReviewRows.Select(r => r.Row.RowIndex));
        Assert.Equal(new int?[] { 0, 1, 2 }, detail.Movements.Select(m => m.SourceRowIndex).OrderBy(i => i));
    }

    [Theory]
    [InlineData("sample_inventory_movements.xlsx", SourceType.Excel)]
    [InlineData("sample_inventory_movements.csv", SourceType.Csv)]
    public async Task Audit_fixture_imports_all_rows_adjustments_locations_and_local_currency(string fileName, SourceType sourceType)
    {
        using var db = new TestDb();
        var service = BuildService(db);

        var preview = (await service.PrepareAsync(new UploadedFile
        {
            FileName = fileName,
            Content = ReadAuditFixture(fileName),
            ContentType = sourceType == SourceType.Csv ? "text/csv" : ""
        })).Single();

        Assert.Equal(sourceType, preview.SourceType);
        Assert.Equal(RecordType.Mixed, preview.Resolution.Proposal.DetectedRecordType);
        Assert.Equal("Almacen", preview.Resolution.Proposal.Column(CanonicalField.Location));
        Assert.Equal("MXN", preview.Resolution.Proposal.Hints.DefaultCurrency);

        var result = await service.CommitAsync(preview, preview.Resolution.Proposal, fileName, saveProfile: false);

        Assert.Equal(7, result.ImportedRows);
        Assert.Equal(0, result.SkippedRows);

        using (var ctx = db.CreateDbContext())
        {
            Assert.Equal(7, ctx.RawRecords.Count());
            Assert.Equal(7, ctx.Movements.Count());
        }

        var tracking = new TrackingService(db);
        var levels = await tracking.GetStockLevelsAsync();
        Assert.Equal(18m, levels.Single(l => l.Sku == "CAF-250").OnHand);
        Assert.Equal(15m, levels.Single(l => l.Sku == "AZU-001").OnHand);
        Assert.Equal(165m, levels.Single(l => l.Sku == "VAS-012").OnHand);

        var byLocation = await tracking.GetStockLevelsByLocationAsync();
        Assert.Equal(23m, byLocation.Single(l => l.Sku == "CAF-250" && l.LocationName == "Bodega").OnHand);
        Assert.Equal(-5m, byLocation.Single(l => l.Sku == "CAF-250" && l.LocationName == "Tienda").OnHand);
        Assert.Equal(18m, byLocation.Single(l => l.Sku == "AZU-001" && l.LocationName == "Bodega").OnHand);
        Assert.Equal(-3m, byLocation.Single(l => l.Sku == "AZU-001" && l.LocationName == "Tienda").OnHand);
        Assert.Equal(200m, byLocation.Single(l => l.Sku == "VAS-012" && l.LocationName == "Bodega").OnHand);
        Assert.Equal(-35m, byLocation.Single(l => l.Sku == "VAS-012" && l.LocationName == "Tienda").OnHand);

        var movements = await tracking.GetRecentMovementsAsync();
        var adjustment = movements.Single(m => m.Product!.Sku == "CAF-250" && m.Kind == MovementKind.Adjustment);
        Assert.Equal(-1m, adjustment.Quantity);
        Assert.Equal("Bodega", adjustment.LocationName);
        Assert.Equal("MXN", adjustment.Currency);

        Assert.Contains(movements, m => m.Product!.Sku == "CAF-250" && m.Kind == MovementKind.In && m.UnitPrice == 68.5m);
        Assert.All(movements, m => Assert.Equal("MXN", m.Currency));
    }

    [Fact]
    public async Task Audit_fixture_stages_ready_rows_then_applies_with_expected_balances()
    {
        using var db = new TestDb();
        var service = BuildService(db);

        var preview = (await service.PrepareAsync(new UploadedFile
        {
            FileName = "sample_inventory_movements.xlsx",
            Content = ReadAuditFixture("sample_inventory_movements.xlsx")
        })).Single();

        var staged = await service.StageAsync(preview, preview.Resolution.Proposal, "Audit", saveProfile: false);

        Assert.Equal(7, staged.ReadyRows);
        Assert.Equal(0, staged.NeedsReviewRows);

        using (var ctx = db.CreateDbContext())
        {
            Assert.Equal(7, ctx.ReviewRows.Count(r => r.Status == ReviewRowStatus.Ready));
            Assert.All(ctx.ReviewRows, r => Assert.True(r.Confidence >= RowConfidenceScorer.ReadyThreshold));
        }

        var applied = await service.ApplyReadyRowsAsync(staged.BatchId);
        Assert.Equal(7, applied.ImportedRows);
        Assert.Equal(0, applied.SkippedRows);

        using (var ctx = db.CreateDbContext())
        {
            Assert.Equal(7, ctx.ReviewRows.Count(r => r.Status == ReviewRowStatus.Applied));
        }

        var levels = await new TrackingService(db).GetStockLevelsAsync();
        Assert.Equal(18m, levels.Single(l => l.Sku == "CAF-250").OnHand);
        Assert.Equal(15m, levels.Single(l => l.Sku == "AZU-001").OnHand);
        Assert.Equal(165m, levels.Single(l => l.Sku == "VAS-012").OnHand);
    }

    [Fact]
    public async Task Human_edit_to_ready_sets_confidence_to_one()
    {
        using var db = new TestDb();
        var service = BuildService(db);

        var preview = (await service.PrepareAsync(new UploadedFile
        {
            FileName = "compras.csv",
            ContentType = "text/csv",
            Content = Encoding.UTF8.GetBytes("Producto,Cantidad\r\nManzana,\r\n")
        })).Single();

        var staged = await service.StageAsync(preview, preview.Resolution.Proposal, "Compras", saveProfile: false);
        Assert.Equal(0, staged.ReadyRows);
        Assert.Equal(1, staged.NeedsReviewRows);

        ReviewRow row;
        using (var ctx = db.CreateDbContext())
        {
            row = ctx.ReviewRows.Single();
            Assert.NotEqual(1.0, row.Confidence);
        }

        await service.UpdateReviewRowAsync(new ReviewRowEdit
        {
            Id = row.Id,
            Status = ReviewRowStatus.Ready,
            ProductName = row.ProductName,
            Sku = row.Sku,
            Category = row.Category,
            Unit = row.Unit,
            Kind = row.Kind,
            Quantity = 12,
            UnitPrice = row.UnitPrice,
            Currency = row.Currency,
            OccurredOn = row.OccurredOn,
            LocationName = row.LocationName,
            PartyName = row.PartyName,
            Note = row.Note
        });

        using (var ctx = db.CreateDbContext())
        {
            var edited = ctx.ReviewRows.Single();
            Assert.Equal(ReviewRowStatus.Ready, edited.Status);
            Assert.Equal(1.0, edited.Confidence);
        }
    }

    private static byte[] ReadAuditFixture(string fileName)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "tests", "InfoOrganizer.Tests", "Fixtures", fileName);
            if (File.Exists(path))
                return File.ReadAllBytes(path);
        }

        throw new FileNotFoundException($"Could not find audit fixture {fileName}.");
    }

    private static ImportPreview ReviewGatePreview()
    {
        var table = new RawTable
        {
            Meta = new SourceMeta { SourceType = SourceType.Image, FileName = "photo.png" },
            Columns = new List<RawColumn>
            {
                new() { Name = "Product" },
                new() { Name = "Qty" }
            },
            Rows = new List<RawRow>
            {
                new()
                {
                    Index = 0,
                    Cells = new Dictionary<string, string?>
                    {
                        ["Product"] = "Widget",
                        ["Qty"] = "7"
                    }
                },
                new()
                {
                    Index = 1,
                    Cells = new Dictionary<string, string?>
                    {
                        ["Product"] = "Bolt",
                        ["Qty"] = ""
                    }
                }
            }
        };

        var proposal = new MappingProposal
        {
            DetectedRecordType = RecordType.Arrivals,
            Fields = new List<FieldMapping>
            {
                new() { Field = CanonicalField.ProductName, SourceColumn = "Product", Confidence = 0.1 },
                new() { Field = CanonicalField.Quantity, SourceColumn = "Qty", Confidence = 0.1 }
            }
        };

        return new ImportPreview
        {
            SourceType = SourceType.Image,
            FileName = "photo.png",
            Table = table,
            Resolution = new MappingResolution
            {
                Fingerprint = "photo-low-confidence",
                Proposal = proposal,
                RequiresConfirmation = true,
                Source = MappingSource.Ai
            }
        };
    }
}
