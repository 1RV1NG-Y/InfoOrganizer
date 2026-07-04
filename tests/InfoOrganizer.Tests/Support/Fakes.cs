using InfoOrganizer.Ai;
using InfoOrganizer.Domain;

namespace InfoOrganizer.Tests.Support;

public sealed class FakeProfileStore : ISourceProfileStore
{
    private readonly Dictionary<string, SourceProfile> _byFingerprint = new();

    public FakeProfileStore(params SourceProfile[] seed)
    {
        foreach (var p in seed) _byFingerprint[p.Fingerprint] = p;
    }

    public Task<SourceProfile?> FindByFingerprintAsync(string fingerprint, CancellationToken ct = default) =>
        Task.FromResult(_byFingerprint.GetValueOrDefault(fingerprint));

    public Task<SourceProfile> SaveAsync(SourceProfile profile, CancellationToken ct = default)
    {
        _byFingerprint[profile.Fingerprint] = profile;
        return Task.FromResult(profile);
    }
}

public sealed class FakeAiClient : IAiClient
{
    public bool IsConfigured { get; init; }
    public MappingProposal? Proposal { get; init; }
    public RawTable? ExtractedTable { get; init; }

    public Task<MappingProposal> ProposeMappingAsync(RawTable table, CancellationToken ct = default) =>
        Proposal is null ? throw new InvalidOperationException("no proposal configured") : Task.FromResult(Proposal);

    public Task<RawTable> ExtractTableFromImageAsync(byte[] imageBytes, string mediaType, string fileName, CancellationToken ct = default) =>
        ExtractedTable is null ? throw new NotSupportedException() : Task.FromResult(ExtractedTable);
}

public static class Tables
{
    public static RawTable Make(string fileName, params (string Name, RawCellType Type)[] columns)
    {
        return new RawTable
        {
            Meta = new SourceMeta { FileName = fileName, SourceType = SourceType.Excel },
            Columns = columns.Select(c => new RawColumn { Name = c.Name, InferredType = c.Type }).ToList()
        };
    }
}
