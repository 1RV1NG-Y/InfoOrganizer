using InfoOrganizer.Data;
using InfoOrganizer.Domain;
using Microsoft.EntityFrameworkCore;

namespace InfoOrganizer.Application;

public sealed record StockLevel(
    int ProductId, string Name, string? Sku, string? Category, string? Unit,
    decimal OnHand, decimal? ReorderThreshold, bool IsLow,
    decimal? UnitCost, decimal? Value, string? CostCurrency);

public sealed record LocationStockLevel(
    int ProductId, string Name, string? Sku, string? Category, string? Unit, string LocationName,
    decimal OnHand, decimal? ReorderThreshold, bool IsLow);

public sealed record TrackingSummary(
    int ProductCount, int LowStockCount, decimal TotalIn, decimal TotalOut, int MovementCount,
    decimal PurchasesValue, decimal SalesValue,
    int PricedInCount, int InCount, int PricedOutCount, int OutCount,
    IReadOnlyList<CurrencyValue> PurchaseValuesByCurrency,
    IReadOnlyList<CurrencyValue> SalesValuesByCurrency);

public sealed record CurrencyValue(string Currency, decimal Value);

/// <summary>Read-side queries that derive the tracking views. Stock on hand is the signed sum of a
/// product's movements, computed in memory (fine at small-org scale and avoids SQLite decimal-aggregation quirks).</summary>
public sealed class TrackingService
{
    private const string DefaultCurrency = "MXN";

    private readonly IDbContextFactory<AppDbContext> _factory;

    public TrackingService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<IReadOnlyList<StockLevel>> GetStockLevelsAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var products = await db.Products.AsNoTracking().ToListAsync(ct);
        var movements = await db.Movements.AsNoTracking()
            .Select(m => new MovementSnapshot(
                m.Id, m.ProductId, m.Kind, m.Quantity, m.UnitPrice, m.Currency, m.OccurredOn))
            .ToListAsync(ct);

        var onHand = movements
            .GroupBy(m => m.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(x => Signed(x.Kind, x.Quantity)));
        var latestCosts = ComputeLatestKnownCosts(movements);

        return products
            .Select(p =>
            {
                var qty = onHand.GetValueOrDefault(p.Id, 0m);
                var low = p.ReorderThreshold is { } t && qty <= t;
                var hasCost = latestCosts.TryGetValue(p.Id, out var cost);
                var unitCost = hasCost ? cost!.UnitCost : (decimal?)null;
                var value = unitCost is { } basis ? qty * basis : (decimal?)null;
                return new StockLevel(
                    p.Id, p.Name, p.Sku, p.Category, p.Unit, qty, p.ReorderThreshold, low,
                    unitCost, value, hasCost ? cost!.Currency : null);
            })
            .OrderBy(s => s.Name)
            .ToList();
    }

    public async Task<IReadOnlyList<StockLevel>> GetLowStockAsync(CancellationToken ct = default) =>
        (await GetStockLevelsAsync(ct)).Where(s => s.IsLow).ToList();

    public async Task<IReadOnlyList<LocationStockLevel>> GetStockLevelsByLocationAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var products = await db.Products.AsNoTracking().ToDictionaryAsync(p => p.Id, ct);
        var movements = await db.Movements.AsNoTracking()
            .Select(m => new { m.ProductId, m.Kind, m.Quantity, m.LocationName })
            .ToListAsync(ct);

        return movements
            .GroupBy(m => new { m.ProductId, LocationName = NormalizeLocation(m.LocationName) })
            .Select(g =>
            {
                var product = products[g.Key.ProductId];
                var qty = g.Sum(x => Signed(x.Kind, x.Quantity));
                var low = product.ReorderThreshold is { } t && qty <= t;
                return new LocationStockLevel(
                    product.Id, product.Name, product.Sku, product.Category, product.Unit,
                    g.Key.LocationName, qty, product.ReorderThreshold, low);
            })
            .OrderBy(s => s.Name)
            .ThenBy(s => s.LocationName)
            .ToList();
    }

    public async Task<TrackingSummary> GetSummaryAsync(DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default)
    {
        var levels = await GetStockLevelsAsync(ct);
        await using var db = await _factory.CreateDbContextAsync(ct);
        var allMovements = await db.Movements.AsNoTracking()
            .Select(m => new MovementSnapshot(
                m.Id, m.ProductId, m.Kind, m.Quantity, m.UnitPrice, m.Currency, m.OccurredOn))
            .ToListAsync(ct);
        var hasPeriodFilter = from is not null || to is not null;
        var movements = allMovements
            .Where(m => IsInPeriod(m, from, to, hasPeriodFilter))
            .ToList();
        var inMovements = movements.Where(m => m.Kind == MovementKind.In).ToList();
        var outMovements = movements.Where(m => m.Kind == MovementKind.Out).ToList();
        var purchaseValuesByCurrency = ValuesByCurrency(inMovements);
        var salesValuesByCurrency = ValuesByCurrency(outMovements);

        return new TrackingSummary(
            ProductCount: levels.Count,
            LowStockCount: levels.Count(l => l.IsLow),
            TotalIn: inMovements.Sum(m => m.Quantity),
            TotalOut: outMovements.Sum(m => m.Quantity),
            MovementCount: movements.Count,
            PurchasesValue: purchaseValuesByCurrency.Sum(v => v.Value),
            SalesValue: salesValuesByCurrency.Sum(v => v.Value),
            PricedInCount: inMovements.Count(m => m.UnitPrice is not null),
            InCount: inMovements.Count,
            PricedOutCount: outMovements.Count(m => m.UnitPrice is not null),
            OutCount: outMovements.Count,
            PurchaseValuesByCurrency: purchaseValuesByCurrency,
            SalesValuesByCurrency: salesValuesByCurrency);
    }

    public async Task<IReadOnlyList<StockMovement>> GetRecentMovementsAsync(int take = 200, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Movements.AsNoTracking()
            .Include(m => m.Product)
            .Include(m => m.ImportBatch)
            .OrderByDescending(m => m.Id)
            .Take(take)
            .ToListAsync(ct);
    }

    private static decimal Signed(MovementKind kind, decimal quantity) => kind switch
    {
        MovementKind.In => quantity,
        MovementKind.Out => -quantity,
        _ => quantity
    };

    private static bool IsInPeriod(MovementSnapshot movement, DateOnly? from, DateOnly? to, bool hasPeriodFilter)
    {
        if (!hasPeriodFilter)
            return true;
        if (movement.OccurredOn is not { } occurredOn)
            return false;

        return (from is null || occurredOn >= from.Value)
            && (to is null || occurredOn <= to.Value);
    }

    /// <summary>
    /// Computes cost basis v1 as latest known unit cost per product from the most recent priced
    /// incoming movement, ordered by date with null dates last and then by Id. This intentionally
    /// uses latest-cost now; weighted-average and FIFO costing are future refinements.
    /// </summary>
    private static IReadOnlyDictionary<int, CostBasis> ComputeLatestKnownCosts(IEnumerable<MovementSnapshot> movements) =>
        movements
            .Where(m => m.Kind == MovementKind.In && m.UnitPrice is not null)
            .GroupBy(m => m.ProductId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var latest = g
                        .OrderBy(m => m.OccurredOn is null ? 1 : 0)
                        .ThenByDescending(m => m.OccurredOn)
                        .ThenByDescending(m => m.Id)
                        .First();
                    return new CostBasis(latest.UnitPrice!.Value, NormalizeCurrency(latest.Currency));
                });

    private static IReadOnlyList<CurrencyValue> ValuesByCurrency(IEnumerable<MovementSnapshot> movements) =>
        movements
            .Where(m => m.UnitPrice is not null)
            .GroupBy(m => NormalizeCurrency(m.Currency))
            .Select(g => new
            {
                Currency = g.Key,
                Value = g.Sum(m => m.Quantity * m.UnitPrice!.Value),
                Count = g.Count()
            })
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.Currency)
            .Select(g => new CurrencyValue(g.Currency, g.Value))
            .ToList();

    private static string NormalizeCurrency(string? currency) =>
        string.IsNullOrWhiteSpace(currency) ? DefaultCurrency : currency.Trim().ToUpperInvariant();

    private static string NormalizeLocation(string? location) =>
        string.IsNullOrWhiteSpace(location) ? "Unspecified" : location.Trim();

    private sealed record MovementSnapshot(
        int Id, int ProductId, MovementKind Kind, decimal Quantity, decimal? UnitPrice, string? Currency, DateOnly? OccurredOn);

    private sealed record CostBasis(decimal UnitCost, string Currency);
}
