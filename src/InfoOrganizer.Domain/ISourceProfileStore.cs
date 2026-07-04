namespace InfoOrganizer.Domain;

/// <summary>Persistence port for saved mappings. Implemented in the data layer; consumed by the
/// mapping engine so it can reuse a confirmed mapping the moment a known format reappears.</summary>
public interface ISourceProfileStore
{
    Task<SourceProfile?> FindByFingerprintAsync(string fingerprint, CancellationToken ct = default);
    Task<SourceProfile> SaveAsync(SourceProfile profile, CancellationToken ct = default);
}
