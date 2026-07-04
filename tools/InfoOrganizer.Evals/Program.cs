using InfoOrganizer.Evals.EvalEngine;
using InfoOrganizer.Evals.Generation;

var command = args.FirstOrDefault()?.ToLowerInvariant() ?? "run";
var root = EvalPaths.FindRepositoryRoot();

try
{
    switch (command)
    {
        case "generate":
            FixtureGenerator.Generate(root);
            Console.WriteLine($"Generated eval corpus under {Path.Combine(root, "evals", "fixtures")}.");
            return 0;

        case "run":
            var jsonPath = ParseJsonPath(args.Skip(1).ToArray());
            var report = await new EvalRunner(root).RunAsync();
            Console.WriteLine(report.ToMarkdown());

            if (!string.IsNullOrWhiteSpace(jsonPath))
            {
                var fullPath = Path.GetFullPath(jsonPath, root);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                await File.WriteAllTextAsync(fullPath, EvalJson.Serialize(report));
                Console.WriteLine();
                Console.WriteLine($"Wrote JSON report to {fullPath}.");
            }

            return report.AllNonKnownIssueHardChecksPass ? 0 : 1;

        default:
            Console.Error.WriteLine("Usage: InfoOrganizer.Evals run [--json <path>] | generate");
            return 2;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

static string? ParseJsonPath(string[] args)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] == "--json")
        {
            if (i + 1 >= args.Length)
                throw new ArgumentException("--json requires a path.");
            return args[i + 1];
        }
    }

    return null;
}
