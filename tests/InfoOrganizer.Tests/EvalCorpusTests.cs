using InfoOrganizer.Evals.EvalEngine;

namespace InfoOrganizer.Tests;

public class EvalCorpusTests
{
    [Fact]
    public async Task Eval_corpus_passes_hard_gate()
    {
        var root = EvalPaths.FindRepositoryRoot();
        var report = await new EvalRunner(root).RunAsync();

        var failures = report.Fixtures
            .Where(f => !f.IsKnownIssue && !f.HardChecksPass)
            .Select(f => $"{f.Name}: {f.Notes}")
            .ToList();

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        Assert.True(
            report.Aggregate.MappingAccuracy >= EvalGate.MappingAccuracyFloor,
            $"Mapping accuracy {report.Aggregate.MappingAccuracy:P1} is below floor {EvalGate.MappingAccuracyFloor:P0}.");
    }
}
