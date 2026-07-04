using InfoOrganizer.Domain;

namespace InfoOrganizer.Ingestion;

public interface IColumnProfiler
{
    /// <summary>Fills each column's <see cref="RawColumn.InferredType"/> and sample values in place.</summary>
    RawTable Profile(RawTable table);
}

/// <summary>Infers a best-effort type and sample values per column by probing the actual cell text.
/// Used for both Excel and photo tables, so detection is purely value-based (no Excel cell metadata).</summary>
public sealed class ColumnProfiler : IColumnProfiler
{
    private const int SampleSize = 6;

    public RawTable Profile(RawTable table)
    {
        foreach (var column in table.Columns)
        {
            var values = table.Rows
                .Select(r => r[column.Name])
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!.Trim())
                .ToList();

            column.SampleValues = values.Take(SampleSize).ToList();
            column.InferredType = ValueProbe.InferType(values);
        }

        return table;
    }
}
