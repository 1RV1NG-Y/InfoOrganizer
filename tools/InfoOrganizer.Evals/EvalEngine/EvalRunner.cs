using InfoOrganizer.Application;
using InfoOrganizer.Data;
using InfoOrganizer.Domain;
using InfoOrganizer.Ingestion;
using InfoOrganizer.Mapping;

namespace InfoOrganizer.Evals.EvalEngine;

public sealed class EvalRunner
{
    private readonly string _repositoryRoot;

    public EvalRunner(string repositoryRoot)
    {
        _repositoryRoot = repositoryRoot;
    }

    public async Task<EvalReport> RunAsync(CancellationToken ct = default)
    {
        var fixtures = FixtureLoader.LoadAll(_repositoryRoot);
        var results = new List<FixtureEvalResult>();

        foreach (var fixture in fixtures)
            results.Add(await RunFixtureAsync(fixture, ct));

        return new EvalReport
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            RepositoryRoot = _repositoryRoot,
            Fixtures = results,
            Aggregate = EvalScorer.Aggregate(results)
        };
    }

    private static async Task<FixtureEvalResult> RunFixtureAsync(EvalFixture fixture, CancellationToken ct)
    {
        var result = new FixtureEvalResult
        {
            Name = fixture.Name,
            KnownIssue = fixture.Expected.KnownIssue
        };

        foreach (var source in fixture.SourcePaths)
            result.Sources.Add(await RunSourceAsync(fixture, source, ct));

        EvalScorer.ScoreParity(result);
        return result;
    }

    private static async Task<SourceEvalResult> RunSourceAsync(EvalFixture fixture, string sourcePath, CancellationToken ct)
    {
        using var db = new EvalTestDb();
        var service = BuildService(db);

        var fileName = Path.GetFileName(sourcePath);
        var previews = await service.PrepareAsync(new UploadedFile
        {
            FileName = fileName,
            ContentType = Path.GetExtension(sourcePath).Equals(".csv", StringComparison.OrdinalIgnoreCase) ? "text/csv" : "",
            Content = await File.ReadAllBytesAsync(sourcePath, ct)
        }, ct);

        if (previews.Count != 1)
            throw new InvalidOperationException($"{fixture.Name}/{fileName} produced {previews.Count} previews; eval fixtures expect exactly one table.");

        var preview = previews.Single();
        var proposal = preview.Resolution.Proposal;
        var staged = await service.StageAsync(preview, proposal, fixture.Name, saveProfile: false, ct);
        var review = await service.GetReviewBatchAsync(staged.BatchId, ct)
            ?? throw new InvalidOperationException($"Could not load staged review batch {staged.BatchId}.");
        var applied = await service.ApplyReadyRowsAsync(staged.BatchId, ct);

        var tracking = new TrackingService(db);
        var stock = await tracking.GetStockLevelsAsync(ct);
        var locationStock = await tracking.GetStockLevelsByLocationAsync(ct);
        var movements = await tracking.GetRecentMovementsAsync(take: 1000, ct);

        return EvalScorer.ScoreSource(
            fixture.Expected,
            fileName,
            preview.SourceType,
            proposal,
            staged,
            review.Rows,
            applied,
            stock,
            locationStock,
            movements);
    }

    private static ImportService BuildService(EvalTestDb db)
    {
        var store = new SourceProfileStore(db);
        var engine = new MappingEngine(store, new HeuristicMapper(), new FakeAiClient());
        return new ImportService(
            new ISourceAdapter[] { new ExcelSourceAdapter(), new CsvSourceAdapter() },
            new ColumnProfiler(),
            engine,
            new Normalizer(),
            new RowConfidenceScorer(),
            store,
            db);
    }
}
