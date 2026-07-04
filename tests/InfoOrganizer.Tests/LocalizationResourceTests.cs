using System.Xml.Linq;

namespace InfoOrganizer.Tests;

/// <summary>
/// Guards that the Spanish resource file stays in parity with the neutral (English) one:
/// every neutral key has a non-empty Spanish value, and Spanish adds no orphan keys.
/// The resx files are read straight from the source tree so the test needs no project reference.
/// </summary>
public class LocalizationResourceTests
{
    [Fact]
    public void Spanish_resx_has_a_value_for_every_neutral_key()
    {
        var neutral = LoadResxKeys("AppStrings.resx");
        var spanish = LoadResxKeys("AppStrings.es.resx");

        var missing = neutral.Keys
            .Where(k => !spanish.ContainsKey(k) || string.IsNullOrWhiteSpace(spanish[k]))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0,
            "Spanish resource is missing values for: " + string.Join(", ", missing));
    }

    [Fact]
    public void Spanish_resx_has_no_orphan_keys()
    {
        var neutral = LoadResxKeys("AppStrings.resx");
        var spanish = LoadResxKeys("AppStrings.es.resx");

        var orphans = spanish.Keys
            .Where(k => !neutral.ContainsKey(k))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        Assert.True(orphans.Count == 0,
            "Spanish resource has keys not present in the neutral resource: " + string.Join(", ", orphans));
    }

    private static IReadOnlyDictionary<string, string> LoadResxKeys(string fileName)
    {
        var doc = XDocument.Load(ResolveResxPath(fileName));
        return doc.Root!
            .Elements("data")
            .Where(e => e.Attribute("name") is not null)
            .ToDictionary(
                e => e.Attribute("name")!.Value,
                e => e.Element("value")?.Value ?? "",
                StringComparer.Ordinal);
    }

    private static string ResolveResxPath(string fileName)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "src", "InfoOrganizer.Web", "Resources", fileName);
            if (File.Exists(path))
                return path;
        }

        throw new FileNotFoundException($"Could not find resource file {fileName} in the source tree.");
    }
}
