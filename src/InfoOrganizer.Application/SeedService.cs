using InfoOrganizer.Data;
using InfoOrganizer.Domain;
using Microsoft.EntityFrameworkCore;

namespace InfoOrganizer.Application;

/// <summary>Loads a small, realistic sample so the app is usable (and demoable) before any real import.</summary>
public sealed class SeedService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public SeedService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<bool> SeedSampleAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        if (await db.Products.AnyAsync(ct)) return false;

        var batch = new ImportBatch
        {
            SourceType = SourceType.Excel,
            FileName = "sample-data",
            Status = ImportStatus.Imported,
            RowCount = 0
        };
        db.ImportBatches.Add(batch);

        // (sku, name, category, unit, reorderAt, received, sold)
        var items = new (string Sku, string Name, string Category, string Unit, decimal Reorder, decimal In, decimal Out)[]
        {
            ("APL", "Manzana", "Fruta", "kg", 20, 100, 30),
            ("PER", "Pera", "Fruta", "kg", 15, 60, 35),
            ("BAN", "Platano", "Fruta", "kg", 10, 40, 12),
            ("MLK", "Leche", "Lacteos", "L", 12, 48, 20),
            ("BRD", "Pan", "Panaderia", "u", 30, 50, 46), // ends below threshold -> low
        };

        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
        foreach (var it in items)
        {
            var product = new Product
            {
                Sku = it.Sku, Name = it.Name, Category = it.Category, Unit = it.Unit, ReorderThreshold = it.Reorder
            };
            db.Products.Add(product);

            batch.Movements.Add(new StockMovement
            {
                Product = product, Kind = MovementKind.In, Quantity = it.In,
                UnitPrice = 1.20m, Currency = "MXN", OccurredOn = date, PartyName = "Proveedor Central"
            });
            if (it.Out > 0)
            {
                batch.Movements.Add(new StockMovement
                {
                    Product = product, Kind = MovementKind.Out, Quantity = it.Out,
                    UnitPrice = 2.00m, Currency = "MXN", OccurredOn = date.AddDays(3), PartyName = "Mostrador"
                });
            }
        }

        batch.RowCount = batch.Movements.Count;
        await db.SaveChangesAsync(ct);
        return true;
    }
}
