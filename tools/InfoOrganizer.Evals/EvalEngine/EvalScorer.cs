using InfoOrganizer.Application;
using InfoOrganizer.Domain;

namespace InfoOrganizer.Evals.EvalEngine;

public static class EvalScorer
{
    public static SourceEvalResult ScoreSource(
        ExpectedOutcome expected,
        string sourceFile,
        SourceType sourceType,
        MappingProposal proposal,
        StagedImportResult staged,
        IReadOnlyList<ReviewRowDetails> reviewRows,
        ImportResult applied,
        IReadOnlyList<StockLevel> stock,
        IReadOnlyList<LocationStockLevel> locationStock,
        IReadOnlyList<StockMovement> movements)
    {
        var result = new SourceEvalResult
        {
            SourceFile = sourceFile,
            SourceType = sourceType.ToString(),
            ExpectedRecordType = expected.RecordType.ToString(),
            ActualRecordType = proposal.DetectedRecordType.ToString(),
            RecordTypePass = proposal.DetectedRecordType == expected.RecordType,
            StagedRows = staged.RowCount,
            ReadyRows = staged.ReadyRows,
            NeedsReviewRows = staged.NeedsReviewRows,
            AppliedRows = applied.ImportedRows,
            ExpectedFlaggedRows = expected.FlaggedRowIndexes.ToList(),
            ActualFlaggedRows = reviewRows
                .Where(r => r.Row.Status == ReviewRowStatus.NeedsReview)
                .Select(r => r.Row.RowIndex)
                .Order()
                .ToList(),
            Stock = stock.Select(s => new ActualStockLevel(s.Name, s.Sku, s.OnHand)).ToList(),
            StockByLocation = locationStock
                .Select(s => new ActualLocationStockLevel(s.Name, s.Sku, s.LocationName, s.OnHand))
                .ToList(),
            MovementCurrencies = movements.OrderBy(m => m.Id).Select(m => m.Currency).ToList()
        };

        ScoreMapping(expected, proposal, result);
        ScoreStock(expected, stock, result);
        ScoreLocationStock(expected, locationStock, result);
        ScoreFlagsAndCounts(expected, result);
        ScoreCurrency(expected, result);

        return result;
    }

    public static EvalAggregate Aggregate(IReadOnlyList<FixtureEvalResult> fixtures)
    {
        var sourceResults = fixtures.SelectMany(f => f.Sources).ToList();
        return new EvalAggregate
        {
            MappingCorrect = sourceResults.Sum(s => s.MappingCorrect),
            MappingTotal = sourceResults.Sum(s => s.MappingTotal),
            StockPassCount = sourceResults.Count(s => s.StockPass && s.LocationStockPass),
            StockCheckCount = sourceResults.Count,
            FlagPassCount = sourceResults.Count(s => s.FlagsPass && s.StagedRowsPass && s.AppliedRowsPass),
            FlagCheckCount = sourceResults.Count
        };
    }

    public static void ScoreParity(FixtureEvalResult fixture)
    {
        if (fixture.Sources.Count < 2)
        {
            fixture.ParityPass = true;
            return;
        }

        var baseline = OutcomeSignature(fixture.Sources[0]);
        for (var i = 1; i < fixture.Sources.Count; i++)
        {
            var next = OutcomeSignature(fixture.Sources[i]);
            if (baseline == next) continue;

            fixture.ParityPass = false;
            fixture.ParityMismatches.Add($"{fixture.Sources[0].SourceFile} != {fixture.Sources[i].SourceFile}");
        }
    }

    private static void ScoreMapping(ExpectedOutcome expected, MappingProposal proposal, SourceEvalResult result)
    {
        foreach (var (fieldName, expectedColumn) in expected.Mapping.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<CanonicalField>(fieldName, ignoreCase: true, out var field))
                throw new InvalidOperationException($"Unknown canonical field in expected mapping: {fieldName}.");

            result.MappingTotal++;
            var actual = proposal.Column(field);
            if (ColumnEquals(expectedColumn, actual))
            {
                result.MappingCorrect++;
            }
            else
            {
                result.MappingMismatches.Add(
                    $"{result.SourceFile}: {field} expected {Display(expectedColumn)}, got {Display(actual)}");
            }
        }
    }

    private static void ScoreStock(ExpectedOutcome expected, IReadOnlyList<StockLevel> stock, SourceEvalResult result)
    {
        var matched = new HashSet<int>();
        foreach (var item in expected.Stock)
        {
            var actual = FindStock(stock, item);
            if (actual is null)
            {
                result.StockMismatches.Add($"{result.SourceFile}: missing stock {item.Key}");
                continue;
            }

            matched.Add(actual.ProductId);
            if (actual.OnHand != item.ExpectedOnHand)
                result.StockMismatches.Add($"{result.SourceFile}: {item.Key} expected {item.ExpectedOnHand}, got {actual.OnHand}");
        }

        foreach (var extra in stock.Where(s => s.OnHand != 0 && !matched.Contains(s.ProductId)))
            result.StockMismatches.Add($"{result.SourceFile}: unexpected stock {extra.Sku ?? extra.Name}={extra.OnHand}");

        result.StockPass = result.StockMismatches.Count == 0;
    }

    private static void ScoreLocationStock(ExpectedOutcome expected, IReadOnlyList<LocationStockLevel> stock, SourceEvalResult result)
    {
        if (expected.StockByLocation.Count == 0)
        {
            result.LocationStockPass = true;
            return;
        }

        var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in expected.StockByLocation)
        {
            var actual = stock.FirstOrDefault(s =>
                ProductMatches(s.Name, s.Sku, item.Product) &&
                string.Equals(s.LocationName, item.Location, StringComparison.OrdinalIgnoreCase));
            if (actual is null)
            {
                result.LocationStockMismatches.Add($"{result.SourceFile}: missing location stock {item.Product}@{item.Location}");
                continue;
            }

            matched.Add($"{actual.ProductId}|{actual.LocationName}");
            if (actual.OnHand != item.ExpectedOnHand)
            {
                result.LocationStockMismatches.Add(
                    $"{result.SourceFile}: {item.Product}@{item.Location} expected {item.ExpectedOnHand}, got {actual.OnHand}");
            }
        }

        foreach (var extra in stock.Where(s => s.OnHand != 0 && !matched.Contains($"{s.ProductId}|{s.LocationName}")))
            result.LocationStockMismatches.Add($"{result.SourceFile}: unexpected location stock {extra.Sku ?? extra.Name}@{extra.LocationName}={extra.OnHand}");

        result.LocationStockPass = result.LocationStockMismatches.Count == 0;
    }

    private static void ScoreFlagsAndCounts(ExpectedOutcome expected, SourceEvalResult result)
    {
        result.FlagsPass = expected.FlaggedRowIndexes.SequenceEqual(result.ActualFlaggedRows);
        if (expected.StagedRows is { } expectedStaged)
            result.StagedRowsPass = result.StagedRows == expectedStaged;
        if (expected.AppliedRows is { } expectedApplied)
            result.AppliedRowsPass = result.AppliedRows == expectedApplied;
    }

    private static void ScoreCurrency(ExpectedOutcome expected, SourceEvalResult result)
    {
        if (string.IsNullOrWhiteSpace(expected.DefaultCurrency))
        {
            result.DefaultCurrencyPass = true;
            return;
        }

        result.DefaultCurrencyPass = result.MovementCurrencies.Count > 0 &&
            result.MovementCurrencies.All(c => string.Equals(c, expected.DefaultCurrency, StringComparison.OrdinalIgnoreCase));
    }

    private static StockLevel? FindStock(IReadOnlyList<StockLevel> stock, StockExpectation expectation)
    {
        if (!string.IsNullOrWhiteSpace(expectation.Sku))
            return stock.FirstOrDefault(s => string.Equals(s.Sku, expectation.Sku, StringComparison.OrdinalIgnoreCase));

        return stock.FirstOrDefault(s => string.Equals(s.Name, expectation.ProductName, StringComparison.OrdinalIgnoreCase));
    }

    private static string OutcomeSignature(SourceEvalResult result)
    {
        var stock = result.Stock
            .OrderBy(s => s.Sku ?? s.Name, StringComparer.OrdinalIgnoreCase)
            .Select(s => $"{s.Sku ?? s.Name}:{s.OnHand}");
        var locations = result.StockByLocation
            .OrderBy(s => s.Sku ?? s.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.Location, StringComparer.OrdinalIgnoreCase)
            .Select(s => $"{s.Sku ?? s.Name}@{s.Location}:{s.OnHand}");
        var flags = string.Join(",", result.ActualFlaggedRows);
        return string.Join("|", stock.Concat(locations).Append($"flags:{flags}").Append($"applied:{result.AppliedRows}"));
    }

    private static bool ProductMatches(string name, string? sku, string expected) =>
        string.Equals(sku, expected, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, expected, StringComparison.OrdinalIgnoreCase);

    private static bool ColumnEquals(string? expected, string? actual)
    {
        if (string.IsNullOrWhiteSpace(expected) && string.IsNullOrWhiteSpace(actual))
            return true;

        return string.Equals(expected?.Trim(), actual?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string Display(string? value) => value is null ? "null" : $"\"{value}\"";
}
