using InfoOrganizer.Domain;

namespace InfoOrganizer.Application;

public interface INormalizer
{
    IReadOnlyList<NormalizedRow> Normalize(RawTable table, MappingProposal mapping);
}

/// <summary>Applies a confirmed mapping to every row: pulls each canonical value from its source column,
/// parses it with the mapping's locale hints, decides In/Out/Adjustment, and records issues. Pure —
/// no database access; absolute-count reconciliation happens in the import service.</summary>
public sealed class Normalizer : INormalizer
{
    public IReadOnlyList<NormalizedRow> Normalize(RawTable table, MappingProposal mapping)
    {
        var mappedColumns = mapping.Fields
            .Where(f => f.SourceColumn != null)
            .Select(f => f.SourceColumn!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var rows = new List<NormalizedRow>();
        foreach (var raw in table.Rows)
            rows.Add(NormalizeRow(raw, mapping, mappedColumns));
        return rows;
    }

    private static NormalizedRow NormalizeRow(RawRow raw, MappingProposal mapping, HashSet<string> mappedColumns)
    {
        string? Get(CanonicalField field)
        {
            var col = mapping.Column(field);
            var value = col is null ? null : raw[col];
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        var row = new NormalizedRow
        {
            RowIndex = raw.Index,
            ProductName = Get(CanonicalField.ProductName),
            Sku = Get(CanonicalField.Sku),
            Category = Get(CanonicalField.Category),
            Unit = Get(CanonicalField.Unit),
            LocationName = Get(CanonicalField.Location),
            PartyName = Get(CanonicalField.PartyName),
            Note = Get(CanonicalField.Note),
            Extra = raw.Cells
                .Where(kv => !mappedColumns.Contains(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
                .ToDictionary(kv => kv.Key, kv => kv.Value)
        };

        if (row.Sku is null && row.ProductName is null)
            row.Issues.Add("No product name or code.");

        ParseQuantity(row, Get(CanonicalField.Quantity), mapping.Hints);
        ParsePrice(row, Get(CanonicalField.UnitPrice), Get(CanonicalField.Currency), mapping.Hints);
        ParseDate(row, Get(CanonicalField.Date), mapping.Hints);
        AssignKind(row, mapping, Get(CanonicalField.Direction));

        return row;
    }

    private static void ParseQuantity(NormalizedRow row, string? text, MappingHints hints)
    {
        if (text is null) { row.Issues.Add("Missing quantity."); return; }
        if (!ValueProbe.TryParseDecimal(text, hints.DecimalComma, out var qty))
        {
            row.Issues.Add($"Could not read quantity \"{text}\".");
            return;
        }
        row.Quantity = qty;
    }

    private static void ParsePrice(NormalizedRow row, string? priceText, string? currencyText, MappingHints hints)
    {
        if (priceText is not null)
        {
            if (ValueProbe.TryParseDecimal(priceText, hints.DecimalComma, out var price))
                row.UnitPrice = price;
            else
                row.Warnings.Add("Price could not be parsed");
        }

        row.Currency = NormalizeCurrency(currencyText)
            ?? (priceText is not null ? ValueProbe.DetectCurrency(priceText) : null)
            ?? NormalizeCurrency(hints.DefaultCurrency);
    }

    private static void ParseDate(NormalizedRow row, string? text, MappingHints hints)
    {
        if (text is null) return;
        if (ValueProbe.TryParseDate(text, hints.DateFormat, out var date))
            row.Date = date;
        else
            row.Warnings.Add("Date could not be parsed");
    }

    private static string? NormalizeCurrency(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var detected = ValueProbe.DetectCurrency(text);
        if (detected is not null) return detected;

        var trimmed = text.Trim().ToUpperInvariant();
        return trimmed.Length == 3 && trimmed.All(char.IsLetter) ? trimmed : null;
    }

    private static void AssignKind(NormalizedRow row, MappingProposal mapping, string? directionText)
    {
        switch (mapping.DetectedRecordType)
        {
            case RecordType.Arrivals:
                row.Kind = MovementKind.In;
                row.Quantity = Math.Abs(row.Quantity);
                break;
            case RecordType.Sales:
                row.Kind = MovementKind.Out;
                row.Quantity = Math.Abs(row.Quantity);
                break;
            case RecordType.StockCount:
                row.Kind = MovementKind.Adjustment;
                row.IsAbsoluteCount = true;
                break;
            case RecordType.Mixed:
                var kind = ValueProbe.ClassifyDirection(directionText);
                if (kind is null)
                {
                    row.Issues.Add($"Could not tell movement direction from \"{directionText}\".");
                    row.Kind = MovementKind.Adjustment;
                    row.Quantity = 0;
                }
                else
                {
                    row.Kind = kind.Value;
                    if (row.Kind is not MovementKind.Adjustment)
                        row.Quantity = Math.Abs(row.Quantity);
                }
                break;
            default:
                row.Kind = MovementKind.In;
                row.Quantity = Math.Abs(row.Quantity);
                break;
        }
    }
}
