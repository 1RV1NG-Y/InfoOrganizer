using InfoOrganizer.Ai;
using InfoOrganizer.Domain;

namespace InfoOrganizer.Evals.EvalEngine;

internal sealed class FakeAiClient : IAiClient
{
    public bool IsConfigured => false;

    public Task<MappingProposal> ProposeMappingAsync(RawTable table, CancellationToken ct = default) =>
        throw new InvalidOperationException("Eval harness must run offline with heuristic mapping only.");

    public Task<RawTable> ExtractTableFromImageAsync(byte[] imageBytes, string mediaType, string fileName, CancellationToken ct = default) =>
        throw new NotSupportedException("Eval corpus uses generated spreadsheets only.");
}
