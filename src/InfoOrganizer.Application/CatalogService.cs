using InfoOrganizer.Data;
using InfoOrganizer.Domain;
using Microsoft.EntityFrameworkCore;

namespace InfoOrganizer.Application;

public sealed record ProfileInfo(int Id, string Name, RecordType DefaultRecordType, DateTime CreatedAtUtc);

/// <summary>Write-side helpers for the Settings screen: reorder thresholds and saved-mapping management.</summary>
public sealed class CatalogService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public CatalogService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<IReadOnlyList<Product>> GetProductsAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Products.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);
    }

    public async Task SetReorderThresholdAsync(int productId, decimal? threshold, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var product = await db.Products.FindAsync([productId], ct);
        if (product is null) return;
        product.ReorderThreshold = threshold;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ProfileInfo>> GetProfilesAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.SourceProfiles.AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new ProfileInfo(p.Id, p.Name, p.DefaultRecordType, p.CreatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task DeleteProfileAsync(int profileId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        await db.SourceProfiles.Where(p => p.Id == profileId).ExecuteDeleteAsync(ct);
    }
}
