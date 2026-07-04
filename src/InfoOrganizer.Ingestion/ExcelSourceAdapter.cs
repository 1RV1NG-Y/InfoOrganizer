using ClosedXML.Excel;
using InfoOrganizer.Domain;

namespace InfoOrganizer.Ingestion;

/// <summary>Reads .xlsx/.xlsm workbooks with ClosedXML. Handles real-world mess: leading title rows,
/// blank rows, blank/duplicate headers, and multiple sheets (each non-empty sheet becomes a RawTable).</summary>
public sealed class ExcelSourceAdapter : ISourceAdapter
{
    private const int HeaderSearchDepth = 15;

    public bool CanHandle(UploadedFile file) =>
        file.Extension is ".xlsx" or ".xlsm" or ".xltx";

    public Task<IReadOnlyList<RawTable>> ExtractAsync(UploadedFile file, CancellationToken ct = default)
    {
        var tables = new List<RawTable>();
        using var stream = new MemoryStream(file.Content);
        using var workbook = new XLWorkbook(stream);

        foreach (var sheet in workbook.Worksheets)
        {
            ct.ThrowIfCancellationRequested();
            var table = ReadSheet(sheet, file.FileName);
            if (table is not null) tables.Add(table);
        }

        return Task.FromResult<IReadOnlyList<RawTable>>(tables);
    }

    private static RawTable? ReadSheet(IXLWorksheet sheet, string fileName)
    {
        var used = sheet.RangeUsed();
        if (used is null) return null;

        int firstRow = used.RangeAddress.FirstAddress.RowNumber;
        int lastRow = used.RangeAddress.LastAddress.RowNumber;
        int firstCol = used.RangeAddress.FirstAddress.ColumnNumber;
        int lastCol = used.RangeAddress.LastAddress.ColumnNumber;

        int headerRow = DetectHeaderRow(sheet, firstRow, lastRow, firstCol, lastCol);
        if (headerRow < 0) return null;

        var headers = BuildHeaders(sheet, headerRow, firstCol, lastCol);
        if (headers.Count < 1) return null;

        var table = new RawTable
        {
            Meta = new SourceMeta { SourceType = SourceType.Excel, FileName = fileName, SheetName = sheet.Name },
            Columns = headers.Select(h => new RawColumn { Name = h.Name }).ToList()
        };

        int index = 0;
        for (int r = headerRow + 1; r <= lastRow; r++)
        {
            var cells = new Dictionary<string, string?>();
            bool anyValue = false;
            foreach (var (name, col) in headers)
            {
                var cell = sheet.Cell(r, col);
                var text = cell.IsEmpty() ? null : cell.GetFormattedString();
                if (!string.IsNullOrWhiteSpace(text)) anyValue = true;
                cells[name] = text;
            }
            if (!anyValue) continue;
            table.Rows.Add(new RawRow { Index = index++, Cells = cells });
        }

        return table.Rows.Count > 0 ? table : null;
    }

    /// <summary>The header is the row (within the first few used rows) with the most non-empty cells,
    /// which steps over title banners and blank spacer rows. Returns -1 if no plausible header.</summary>
    private static int DetectHeaderRow(IXLWorksheet sheet, int firstRow, int lastRow, int firstCol, int lastCol)
    {
        int bestRow = -1, bestCount = 1; // require at least 2 filled cells to count as a header
        int limit = Math.Min(firstRow + HeaderSearchDepth - 1, lastRow);

        for (int r = firstRow; r <= limit; r++)
        {
            int count = 0;
            for (int c = firstCol; c <= lastCol; c++)
                if (!sheet.Cell(r, c).IsEmpty()) count++;

            if (count > bestCount)
            {
                bestCount = count;
                bestRow = r;
            }
        }

        // Need at least one data row beneath the header.
        return bestRow > 0 && bestRow < lastRow ? bestRow : -1;
    }

    private static List<(string Name, int Col)> BuildHeaders(IXLWorksheet sheet, int headerRow, int firstCol, int lastCol)
    {
        var headers = new List<(string Name, int Col)>();
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int c = firstCol; c <= lastCol; c++)
        {
            var raw = sheet.Cell(headerRow, c).GetFormattedString().Trim();
            var name = string.IsNullOrWhiteSpace(raw) ? $"Column{c}" : raw;

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
}
