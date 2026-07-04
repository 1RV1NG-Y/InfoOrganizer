using InfoOrganizer.Domain;

namespace InfoOrganizer.Ai;

/// <summary>The system's single seam to the LLM. Keeps the Anthropic SDK behind an interface so the
/// pipeline can run offline (heuristics only) and so unit tests can substitute a fake.</summary>
public interface IAiClient
{
    /// <summary>True when an API key is configured. When false, callers fall back to heuristics.</summary>
    bool IsConfigured { get; }

    /// <summary>Propose how a source table's columns map onto the canonical schema, plus record type
    /// and locale hints. Used for unknown formats before the user confirms.</summary>
    Task<MappingProposal> ProposeMappingAsync(RawTable table, CancellationToken ct = default);

    /// <summary>Extract a tabular structure from a photo of a paper record into the neutral RawTable shape.</summary>
    Task<RawTable> ExtractTableFromImageAsync(byte[] imageBytes, string mediaType, string fileName, CancellationToken ct = default);
}
