using InfoOrganizer.Domain;
using Microsoft.EntityFrameworkCore;

namespace InfoOrganizer.Data;

public sealed class SourceProfileStore : ISourceProfileStore
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public SourceProfileStore(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<SourceProfile?> FindByFingerprintAsync(string fingerprint, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.SourceProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Fingerprint == fingerprint, ct);
    }

    public async Task<SourceProfile> SaveAsync(SourceProfile profile, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);

        var existing = await db.SourceProfiles.FirstOrDefaultAsync(p => p.Fingerprint == profile.Fingerprint, ct);
        if (existing is null)
        {
            db.SourceProfiles.Add(profile);
        }
        else
        {
            existing.Name = profile.Name;
            existing.MappingJson = profile.MappingJson;
            existing.DefaultRecordType = profile.DefaultRecordType;
            existing.HintsJson = profile.HintsJson;
            profile = existing;
        }

        await db.SaveChangesAsync(ct);
        return profile;
    }
}
