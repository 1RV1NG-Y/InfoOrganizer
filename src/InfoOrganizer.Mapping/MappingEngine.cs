using InfoOrganizer.Ai;
using InfoOrganizer.Domain;

namespace InfoOrganizer.Mapping;

public interface IMappingEngine
{
    /// <summary>Resolve how to interpret a table. Known formats return a saved mapping that needs no
    /// confirmation; new formats return a proposal (AI when configured, else heuristic) for the user to confirm.</summary>
    Task<MappingResolution> ResolveAsync(RawTable table, CancellationToken ct = default);
}

public sealed class MappingEngine : IMappingEngine
{
    private readonly ISourceProfileStore _store;
    private readonly HeuristicMapper _heuristic;
    private readonly IAiClient _ai;

    public MappingEngine(ISourceProfileStore store, HeuristicMapper heuristic, IAiClient ai)
    {
        _store = store;
        _heuristic = heuristic;
        _ai = ai;
    }

    public async Task<MappingResolution> ResolveAsync(RawTable table, CancellationToken ct = default)
    {
        var fingerprint = SchemaFingerprint.Compute(table.ColumnNames);
        var heuristic = _heuristic.Propose(table);

        var saved = await _store.FindByFingerprintAsync(fingerprint, ct);
        if (saved is not null)
        {
            return new MappingResolution
            {
                Fingerprint = fingerprint,
                Proposal = FromProfile(saved, heuristic),
                RequiresConfirmation = false,
                SourceProfileId = saved.Id,
                Source = MappingSource.SavedProfile
            };
        }

        if (_ai.IsConfigured)
        {
            try
            {
                var ai = await _ai.ProposeMappingAsync(table, ct);
                return new MappingResolution
                {
                    Fingerprint = fingerprint,
                    Proposal = MergeFallback(ai, heuristic),
                    RequiresConfirmation = true,
                    Source = MappingSource.Ai
                };
            }
            catch
            {
                // Network/parse failure — degrade gracefully to the heuristic proposal.
            }
        }

        return new MappingResolution
        {
            Fingerprint = fingerprint,
            Proposal = heuristic,
            RequiresConfirmation = true,
            Source = MappingSource.Heuristic
        };
    }

    private static MappingProposal FromProfile(SourceProfile profile, MappingProposal heuristic)
    {
        var fields = MappingSerializer.DeserializeFields(profile.MappingJson);
        foreach (var h in heuristic.Fields.Where(h => h.SourceColumn != null))
        {
            if (fields.All(f => f.Field != h.Field))
                fields.Add(new FieldMapping { Field = h.Field, SourceColumn = h.SourceColumn, Confidence = h.Confidence * 0.8 });
        }

        foreach (var info in CanonicalSchema.Fields)
        {
            if (fields.All(f => f.Field != info.Field))
                fields.Add(new FieldMapping { Field = info.Field, SourceColumn = null, Confidence = 0 });
        }

        var hints = MappingSerializer.DeserializeHints(profile.HintsJson);
        hints.DateFormat ??= heuristic.Hints.DateFormat;
        hints.DefaultCurrency ??= heuristic.Hints.DefaultCurrency;

        return new MappingProposal
        {
            Fields = fields,
            DetectedRecordType = profile.DefaultRecordType,
            Hints = hints,
            OverallConfidence = 1.0,
            Rationale = $"Reused saved profile \"{profile.Name}\"."
        };
    }

    /// <summary>Fill any field the AI left unmapped with the heuristic's guess, and backfill an
    /// Unknown record type / empty hints from the heuristic.</summary>
    private static MappingProposal MergeFallback(MappingProposal ai, MappingProposal heuristic)
    {
        foreach (var h in heuristic.Fields.Where(h => h.SourceColumn != null))
        {
            var existing = ai.Fields.FirstOrDefault(f => f.Field == h.Field);
            if (existing is null)
                ai.Fields.Add(new FieldMapping { Field = h.Field, SourceColumn = h.SourceColumn, Confidence = h.Confidence * 0.8 });
            else if (existing.SourceColumn is null)
            {
                existing.SourceColumn = h.SourceColumn;
                existing.Confidence = h.Confidence * 0.8;
            }
        }

        if (ai.DetectedRecordType == RecordType.Unknown)
            ai.DetectedRecordType = heuristic.DetectedRecordType;

        ai.Hints.DefaultCurrency ??= heuristic.Hints.DefaultCurrency;
        ai.Hints.DateFormat ??= heuristic.Hints.DateFormat;

        return ai;
    }
}
