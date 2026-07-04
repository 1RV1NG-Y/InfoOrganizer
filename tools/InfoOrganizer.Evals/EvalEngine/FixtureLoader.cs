namespace InfoOrganizer.Evals.EvalEngine;

public sealed class EvalFixture
{
    public string Name { get; init; } = "";
    public string DirectoryPath { get; init; } = "";
    public List<string> SourcePaths { get; init; } = new();
    public ExpectedOutcome Expected { get; init; } = new();
}

public static class FixtureLoader
{
    public static IReadOnlyList<EvalFixture> LoadAll(string repositoryRoot)
    {
        var fixturesRoot = EvalPaths.FixturesDirectory(repositoryRoot);
        if (!Directory.Exists(fixturesRoot))
            throw new DirectoryNotFoundException($"Eval fixture directory not found: {fixturesRoot}. Run `dotnet run --project tools/InfoOrganizer.Evals -- generate`.");

        return Directory.GetDirectories(fixturesRoot)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(Load)
            .ToList();
    }

    private static EvalFixture Load(string directory)
    {
        var expectedPath = Path.Combine(directory, "expected.json");
        if (!File.Exists(expectedPath))
            throw new FileNotFoundException($"Missing expected.json for fixture {directory}.", expectedPath);

        var expected = EvalJson.Deserialize<ExpectedOutcome>(File.ReadAllText(expectedPath));
        expected.Normalize();

        var sources = new[] { "source.xlsx", "source.csv" }
            .Select(name => Path.Combine(directory, name))
            .Where(File.Exists)
            .ToList();

        if (sources.Count == 0)
            throw new FileNotFoundException($"Fixture {directory} has no source.xlsx or source.csv.");

        return new EvalFixture
        {
            Name = Path.GetFileName(directory),
            DirectoryPath = directory,
            SourcePaths = sources,
            Expected = expected
        };
    }
}
