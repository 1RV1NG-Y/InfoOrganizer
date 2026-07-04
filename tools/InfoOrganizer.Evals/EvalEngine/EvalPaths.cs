namespace InfoOrganizer.Evals.EvalEngine;

public static class EvalPaths
{
    public static string FindRepositoryRoot(string? start = null)
    {
        foreach (var candidate in CandidateStarts(start))
        {
            for (var dir = new DirectoryInfo(candidate); dir is not null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "InfoOrganizer.slnx")))
                    return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate repository root containing InfoOrganizer.slnx.");
    }

    public static string FixturesDirectory(string repositoryRoot) =>
        Path.Combine(repositoryRoot, "evals", "fixtures");

    private static IEnumerable<string> CandidateStarts(string? start)
    {
        if (!string.IsNullOrWhiteSpace(start))
            yield return Path.GetFullPath(start);

        yield return Directory.GetCurrentDirectory();
        yield return AppContext.BaseDirectory;
    }
}
