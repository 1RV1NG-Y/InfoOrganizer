namespace InfoOrganizer.Evals.EvalEngine;

public static class EvalGate
{
    public const double MappingAccuracyFloor = 0.90;
}

public sealed class EvalReport
{
    public DateTimeOffset GeneratedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string RepositoryRoot { get; set; } = "";
    public double MappingAccuracyFloor { get; set; } = EvalGate.MappingAccuracyFloor;
    public List<FixtureEvalResult> Fixtures { get; set; } = new();
    public EvalAggregate Aggregate { get; set; } = new();

    public bool AllNonKnownIssueHardChecksPass =>
        Fixtures.Where(f => !f.IsKnownIssue).All(f => f.HardChecksPass);

    public string ToMarkdown()
    {
        var lines = new List<string>
        {
            "| Fixture | Mapping | RecordType | Stock | Flags | Notes |",
            "|---|---:|---|---|---|---|"
        };

        foreach (var fixture in Fixtures.OrderBy(f => f.Name))
        {
            lines.Add(string.Join(" | ", new[]
            {
                $"| {fixture.Name}",
                $"{fixture.MappingAccuracy:P0} ({fixture.MappingCorrect}/{fixture.MappingTotal})",
                Mark(fixture.RecordTypePass),
                Mark(fixture.StockPass),
                Mark(fixture.FlagsPass),
                $"{Escape(fixture.Notes)} |"
            }));
        }

        lines.Add("");
        lines.Add("Aggregate:");
        lines.Add($"- Mapping accuracy: {Aggregate.MappingAccuracy:P1} ({Aggregate.MappingCorrect}/{Aggregate.MappingTotal}); gate floor {MappingAccuracyFloor:P0}");
        lines.Add($"- Stock pass rate: {Aggregate.StockPassRate:P1} ({Aggregate.StockPassCount}/{Aggregate.StockCheckCount})");
        lines.Add($"- Flag pass rate: {Aggregate.FlagPassRate:P1} ({Aggregate.FlagPassCount}/{Aggregate.FlagCheckCount})");
        lines.Add($"- Non-known-issue hard gate: {(AllNonKnownIssueHardChecksPass ? "PASS" : "FAIL")}");

        var known = Fixtures.Where(f => f.IsKnownIssue).ToList();
        if (known.Count > 0)
        {
            lines.Add("");
            lines.Add("Known issues:");
            foreach (var fixture in known)
                lines.Add($"- {fixture.Name}: {fixture.KnownIssue}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string Mark(bool pass) => pass ? "PASS" : "FAIL";

    private static string Escape(string value) => value.Replace("|", "\\|");
}

public sealed class EvalAggregate
{
    public int MappingCorrect { get; set; }
    public int MappingTotal { get; set; }
    public double MappingAccuracy => MappingTotal == 0 ? 1.0 : (double)MappingCorrect / MappingTotal;
    public int StockPassCount { get; set; }
    public int StockCheckCount { get; set; }
    public double StockPassRate => StockCheckCount == 0 ? 1.0 : (double)StockPassCount / StockCheckCount;
    public int FlagPassCount { get; set; }
    public int FlagCheckCount { get; set; }
    public double FlagPassRate => FlagCheckCount == 0 ? 1.0 : (double)FlagPassCount / FlagCheckCount;
}

public sealed class FixtureEvalResult
{
    public string Name { get; set; } = "";
    public string? KnownIssue { get; set; }
    public List<SourceEvalResult> Sources { get; set; } = new();
    public bool ParityPass { get; set; } = true;
    public List<string> ParityMismatches { get; set; } = new();

    public bool IsKnownIssue => !string.IsNullOrWhiteSpace(KnownIssue);
    public int MappingCorrect => Sources.Sum(s => s.MappingCorrect);
    public int MappingTotal => Sources.Sum(s => s.MappingTotal);
    public double MappingAccuracy => MappingTotal == 0 ? 1.0 : (double)MappingCorrect / MappingTotal;
    public bool RecordTypePass => Sources.All(s => s.RecordTypePass);
    public bool StockPass => Sources.All(s => s.StockPass && s.LocationStockPass);
    public bool FlagsPass => Sources.All(s => s.FlagsPass && s.StagedRowsPass && s.AppliedRowsPass);
    public bool CurrencyPass => Sources.All(s => s.DefaultCurrencyPass);
    public bool HardChecksPass => StockPass && FlagsPass && CurrencyPass && ParityPass;

    public string Notes
    {
        get
        {
            if (IsKnownIssue) return $"knownIssue: {KnownIssue}";

            var notes = new List<string>();
            if (!ParityPass) notes.Add("parity failed");
            var mismatches = Sources.SelectMany(s => s.Notes).Distinct().Take(3).ToList();
            notes.AddRange(mismatches);
            return notes.Count == 0 ? "" : string.Join("; ", notes);
        }
    }
}

public sealed class SourceEvalResult
{
    public string SourceFile { get; set; } = "";
    public string SourceType { get; set; } = "";
    public int MappingCorrect { get; set; }
    public int MappingTotal { get; set; }
    public double MappingAccuracy => MappingTotal == 0 ? 1.0 : (double)MappingCorrect / MappingTotal;
    public List<string> MappingMismatches { get; set; } = new();
    public string ExpectedRecordType { get; set; } = "";
    public string ActualRecordType { get; set; } = "";
    public bool RecordTypePass { get; set; }
    public bool StockPass { get; set; }
    public bool LocationStockPass { get; set; } = true;
    public bool FlagsPass { get; set; }
    public bool StagedRowsPass { get; set; } = true;
    public bool AppliedRowsPass { get; set; } = true;
    public bool DefaultCurrencyPass { get; set; } = true;
    public int StagedRows { get; set; }
    public int ReadyRows { get; set; }
    public int NeedsReviewRows { get; set; }
    public int AppliedRows { get; set; }
    public List<int> ExpectedFlaggedRows { get; set; } = new();
    public List<int> ActualFlaggedRows { get; set; } = new();
    public List<ActualStockLevel> Stock { get; set; } = new();
    public List<ActualLocationStockLevel> StockByLocation { get; set; } = new();
    public List<string> StockMismatches { get; set; } = new();
    public List<string> LocationStockMismatches { get; set; } = new();
    public List<string?> MovementCurrencies { get; set; } = new();

    public IEnumerable<string> Notes
    {
        get
        {
            foreach (var item in MappingMismatches.Take(1))
                yield return item;
            if (!RecordTypePass)
                yield return $"{SourceFile}: recordType {ActualRecordType}, expected {ExpectedRecordType}";
            foreach (var item in StockMismatches.Take(1))
                yield return item;
            foreach (var item in LocationStockMismatches.Take(1))
                yield return item;
            if (!FlagsPass)
                yield return $"{SourceFile}: flags expected [{string.Join(",", ExpectedFlaggedRows)}], got [{string.Join(",", ActualFlaggedRows)}]";
            if (!AppliedRowsPass)
                yield return $"{SourceFile}: applied rows {AppliedRows}";
            if (!DefaultCurrencyPass)
                yield return $"{SourceFile}: currency mismatch";
        }
    }
}

public sealed record ActualStockLevel(string Name, string? Sku, decimal OnHand);

public sealed record ActualLocationStockLevel(string Name, string? Sku, string Location, decimal OnHand);
