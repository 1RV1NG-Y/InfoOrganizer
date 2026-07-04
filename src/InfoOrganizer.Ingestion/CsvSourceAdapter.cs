using System.Text;
using InfoOrganizer.Domain;

namespace InfoOrganizer.Ingestion;

/// <summary>Reads delimited text exports into the same neutral table shape used by Excel and images.</summary>
public sealed class CsvSourceAdapter : ISourceAdapter
{
    private const int HeaderSearchDepth = 15;
    private static readonly char[] CandidateDelimiters = { ',', ';', '\t', '|' };

    public bool CanHandle(UploadedFile file) => file.Extension is ".csv" or ".tsv";

    public Task<IReadOnlyList<RawTable>> ExtractAsync(UploadedFile file, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var text = Decode(file.Content);
        var delimiter = file.Extension == ".tsv" ? '\t' : DetectDelimiter(text);
        var parsed = ParseRows(text, delimiter);
        var table = BuildTable(parsed, file.FileName);

        IReadOnlyList<RawTable> result = table is null ? Array.Empty<RawTable>() : new[] { table };
        return Task.FromResult(result);
    }

    private static RawTable? BuildTable(IReadOnlyList<IReadOnlyList<string>> parsed, string fileName)
    {
        var headerRow = DetectHeaderRow(parsed);
        if (headerRow < 0) return null;

        var headers = BuildHeaders(parsed[headerRow]);
        if (headers.Count == 0) return null;

        var table = new RawTable
        {
            Meta = new SourceMeta { SourceType = SourceType.Csv, FileName = fileName },
            Columns = headers.Select(h => new RawColumn { Name = h.Name }).ToList()
        };

        var index = 0;
        for (var r = headerRow + 1; r < parsed.Count; r++)
        {
            var cells = new Dictionary<string, string?>();
            var anyValue = false;
            for (var c = 0; c < headers.Count; c++)
            {
                var value = c < parsed[r].Count ? parsed[r][c] : null;
                if (!string.IsNullOrWhiteSpace(value)) anyValue = true;
                cells[headers[c].Name] = value;
            }

            if (anyValue)
                table.Rows.Add(new RawRow { Index = index++, Cells = cells });
        }

        return table.Rows.Count > 0 ? table : null;
    }

    private static int DetectHeaderRow(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var bestRow = -1;
        var bestCount = 1; // require at least 2 filled cells to count as a header
        var limit = Math.Min(HeaderSearchDepth, rows.Count);

        for (var r = 0; r < limit; r++)
        {
            var count = rows[r].Count(v => !string.IsNullOrWhiteSpace(v));
            if (count > bestCount)
            {
                bestCount = count;
                bestRow = r;
            }
        }

        return bestRow >= 0 && bestRow < rows.Count - 1 ? bestRow : -1;
    }

    private static List<(string Name, int Index)> BuildHeaders(IReadOnlyList<string> row)
    {
        var headers = new List<(string Name, int Index)>();
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var c = 0; c < row.Count; c++)
        {
            var raw = row[c].Trim();
            var name = string.IsNullOrWhiteSpace(raw) ? $"Column{c + 1}" : raw;

            if (seen.TryGetValue(name, out var n))
            {
                seen[name] = ++n;
                name = $"{name} ({n})";
            }
            else
            {
                seen[name] = 1;
            }

            headers.Add((name, c));
        }

        return headers;
    }

    private static char DetectDelimiter(string text)
    {
        return CandidateDelimiters
            .Select(d => new { Delimiter = d, Score = ScoreDelimiter(text, d) })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => Array.IndexOf(CandidateDelimiters, x.Delimiter))
            .First()
            .Delimiter;
    }

    private static int ScoreDelimiter(string text, char delimiter)
    {
        var rows = ParseRows(text, delimiter)
            .Where(r => r.Any(v => !string.IsNullOrWhiteSpace(v)))
            .Take(20)
            .ToList();
        if (rows.Count == 0) return 0;

        var counts = rows.Select(r => r.Count).Where(c => c > 1).ToList();
        if (counts.Count == 0) return 0;

        var mode = counts
            .GroupBy(c => c)
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => g.Key)
            .First()
            .Key;
        var stableRows = counts.Count(c => c == mode);
        var drift = counts.Sum(c => Math.Abs(c - mode));
        var headerBonus = rows[0].Count == mode ? 3 : 0;

        return (mode * 10) + (stableRows * 5) + headerBonus - drift;
    }

    private static List<List<string>> ParseRows(string text, char delimiter)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var sawAny = false;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];

            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(ch);
                }

                sawAny = true;
                continue;
            }

            if (ch == '"' && field.ToString().Trim().Length == 0)
            {
                field.Clear();
                inQuotes = true;
                sawAny = true;
                continue;
            }

            if (ch == delimiter)
            {
                row.Add(field.ToString().Trim());
                field.Clear();
                sawAny = true;
                continue;
            }

            if (ch is '\r' or '\n')
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    i++;

                row.Add(field.ToString().Trim());
                if (sawAny || row.Any(v => !string.IsNullOrWhiteSpace(v)))
                    rows.Add(row);

                row = new List<string>();
                field.Clear();
                sawAny = false;
                continue;
            }

            field.Append(ch);
            if (!char.IsWhiteSpace(ch)) sawAny = true;
        }

        if (sawAny || field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString().Trim());
            rows.Add(row);
        }

        return rows;
    }

    private static string Decode(byte[] content)
    {
        if (content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF)
            return Encoding.UTF8.GetString(content, 3, content.Length - 3);

        if (content.Length >= 2 && content[0] == 0xFF && content[1] == 0xFE)
            return Encoding.Unicode.GetString(content, 2, content.Length - 2);

        if (content.Length >= 2 && content[0] == 0xFE && content[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(content, 2, content.Length - 2);

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false)
            .GetString(content);
    }
}
