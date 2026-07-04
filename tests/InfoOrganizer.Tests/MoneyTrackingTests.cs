using InfoOrganizer.Application;
using InfoOrganizer.Data;
using InfoOrganizer.Domain;
using InfoOrganizer.Ingestion;
using InfoOrganizer.Mapping;
using InfoOrganizer.Tests.Support;

namespace InfoOrganizer.Tests;

public class MoneyTrackingTests
{
    [Fact]
    public async Task Summary_values_skip_unpriced_movements_but_keep_quantity_totals()
    {
        using var db = new TestDb();

        await using (var ctx = db.CreateDbContext())
        {
            var product = new Product { Name = "Widget", Unit = "ea" };
            var batch = ManualBatch();
            ctx.Movements.AddRange(
                new StockMovement
                {
                    Product = product, ImportBatch = batch, Kind = MovementKind.In,
                    Quantity = 10m, UnitPrice = 5m, Currency = "MXN", OccurredOn = new DateOnly(2026, 1, 1)
                },
                new StockMovement
                {
                    Product = product, ImportBatch = batch, Kind = MovementKind.In,
                    Quantity = 3m, Currency = "MXN", OccurredOn = new DateOnly(2026, 1, 2)
                },
                new StockMovement
                {
                    Product = product, ImportBatch = batch, Kind = MovementKind.Out,
                    Quantity = 4m, UnitPrice = 8m, Currency = "MXN", OccurredOn = new DateOnly(2026, 1, 3)
                },
                new StockMovement
                {
                    Product = product, ImportBatch = batch, Kind = MovementKind.Out,
                    Quantity = 2m, Currency = "MXN", OccurredOn = new DateOnly(2026, 1, 4)
                });
            await ctx.SaveChangesAsync();
        }

        var summary = await new TrackingService(db).GetSummaryAsync();

        Assert.Equal(13m, summary.TotalIn);
        Assert.Equal(6m, summary.TotalOut);
        Assert.Equal(4, summary.MovementCount);
        Assert.Equal(50m, summary.PurchasesValue);
        Assert.Equal(32m, summary.SalesValue);
        Assert.Equal(1, summary.PricedInCount);
        Assert.Equal(2, summary.InCount);
        Assert.Equal(1, summary.PricedOutCount);
        Assert.Equal(2, summary.OutCount);
        Assert.Collection(summary.PurchaseValuesByCurrency, value =>
        {
            Assert.Equal("MXN", value.Currency);
            Assert.Equal(50m, value.Value);
        });
        Assert.Collection(summary.SalesValuesByCurrency, value =>
        {
            Assert.Equal("MXN", value.Currency);
            Assert.Equal(32m, value.Value);
        });
    }

    [Fact]
    public async Task Summary_period_filter_excludes_outside_and_null_dated_movements()
    {
        using var db = new TestDb();

        await using (var ctx = db.CreateDbContext())
        {
            var product = new Product { Name = "Widget", Unit = "ea" };
            var batch = ManualBatch();
            ctx.Movements.AddRange(
                new StockMovement
                {
                    Product = product, ImportBatch = batch, Kind = MovementKind.In,
                    Quantity = 10m, UnitPrice = 2m, Currency = "MXN", OccurredOn = new DateOnly(2026, 1, 15)
                },
                new StockMovement
                {
                    Product = product, ImportBatch = batch, Kind = MovementKind.In,
                    Quantity = 20m, UnitPrice = 2m, Currency = "MXN", OccurredOn = new DateOnly(2026, 2, 1)
                },
                new StockMovement
                {
                    Product = product, ImportBatch = batch, Kind = MovementKind.Out,
                    Quantity = 3m, UnitPrice = 5m, Currency = "MXN", OccurredOn = null
                },
                new StockMovement
                {
                    Product = product, ImportBatch = batch, Kind = MovementKind.Out,
                    Quantity = 4m, UnitPrice = 5m, Currency = "MXN", OccurredOn = new DateOnly(2026, 1, 20)
                });
            await ctx.SaveChangesAsync();
        }

        var tracking = new TrackingService(db);
        var allTime = await tracking.GetSummaryAsync();
        var january = await tracking.GetSummaryAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.Equal(4, allTime.MovementCount);
        Assert.Equal(30m, allTime.TotalIn);
        Assert.Equal(7m, allTime.TotalOut);

        Assert.Equal(2, january.MovementCount);
        Assert.Equal(10m, january.TotalIn);
        Assert.Equal(4m, january.TotalOut);
        Assert.Equal(20m, january.PurchasesValue);
        Assert.Equal(20m, january.SalesValue);
        Assert.Equal(1, january.InCount);
        Assert.Equal(1, january.PricedInCount);
        Assert.Equal(1, january.OutCount);
        Assert.Equal(1, january.PricedOutCount);
    }

    [Fact]
    public async Task Stock_levels_use_latest_priced_in_movement_for_valuation()
    {
        using var db = new TestDb();

        await using (var ctx = db.CreateDbContext())
        {
            var priced = new Product { Name = "Priced product", Unit = "ea" };
            var unpriced = new Product { Name = "Unpriced product", Unit = "ea" };
            var batch = ManualBatch();
            ctx.Movements.AddRange(
                new StockMovement
                {
                    Product = priced, ImportBatch = batch, Kind = MovementKind.In,
                    Quantity = 10m, UnitPrice = 10m, Currency = "MXN", OccurredOn = new DateOnly(2026, 1, 1)
                },
                new StockMovement
                {
                    Product = priced, ImportBatch = batch, Kind = MovementKind.In,
                    Quantity = 5m, UnitPrice = 12m, Currency = "MXN", OccurredOn = new DateOnly(2026, 1, 2)
                },
                new StockMovement
                {
                    Product = priced, ImportBatch = batch, Kind = MovementKind.Out,
                    Quantity = 4m, Currency = "MXN", OccurredOn = new DateOnly(2026, 1, 3)
                },
                new StockMovement
                {
                    Product = unpriced, ImportBatch = batch, Kind = MovementKind.In,
                    Quantity = 7m, Currency = "MXN", OccurredOn = new DateOnly(2026, 1, 1)
                });
            await ctx.SaveChangesAsync();
        }

        var levels = await new TrackingService(db).GetStockLevelsAsync();
        var pricedLevel = levels.Single(l => l.Name == "Priced product");
        var unpricedLevel = levels.Single(l => l.Name == "Unpriced product");

        Assert.Equal(11m, pricedLevel.OnHand);
        Assert.Equal(12m, pricedLevel.UnitCost);
        Assert.Equal(132m, pricedLevel.Value);
        Assert.Equal("MXN", pricedLevel.CostCurrency);

        Assert.Equal(7m, unpricedLevel.OnHand);
        Assert.Null(unpricedLevel.UnitCost);
        Assert.Null(unpricedLevel.Value);
    }

    [Fact]
    public async Task Audit_fixture_priced_rows_contribute_purchase_and_sales_values()
    {
        using var db = new TestDb();
        var service = BuildService(db);

        var preview = (await service.PrepareAsync(new UploadedFile
        {
            FileName = "sample_inventory_movements.xlsx",
            Content = ReadAuditFixture("sample_inventory_movements.xlsx")
        })).Single();

        await service.CommitAsync(preview, preview.Resolution.Proposal, "Audit", saveProfile: false);

        var summary = await new TrackingService(db).GetSummaryAsync();

        Assert.True(summary.PurchasesValue > 0m);
        Assert.True(summary.SalesValue > 0m);
        Assert.True(summary.PricedInCount > 0);
        Assert.True(summary.PricedOutCount > 0);
    }

    private static ImportBatch ManualBatch() => new()
    {
        SourceType = SourceType.Csv,
        FileName = "manual.csv",
        RowCount = 0,
        Status = ImportStatus.Imported
    };

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
}
