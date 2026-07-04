using System.Globalization;
using System.Text;
using InfoOrganizer.Domain;

namespace InfoOrganizer.Mapping;

/// <summary>Deterministic, offline mapper: matches source headers to canonical fields by multilingual
/// synonyms and column type. Serves both as a pre-fill for the AI proposer and as the fallback when no
/// API key is configured or the AI call fails.</summary>
public sealed class HeuristicMapper
{
    private const double Threshold = 0.55;
    private const double DirectionSampleDominanceThreshold = 0.60;
    private const double DirectionSampleScore = 0.98;
    private const double CategoryDirectionSampleCeiling = 0.35;

    private static readonly Dictionary<CanonicalField, string[]> Synonyms = new()
    {
        [CanonicalField.ProductName] = new[] { "product", "item", "name", "producto", "articulo", "descripcion", "description", "nombre", "material", "concepto" },
        [CanonicalField.Sku] = new[] { "sku", "code", "codigo", "barcode", "ref", "reference", "id", "item code", "upc", "clave", "cve", "numero" },
        [CanonicalField.Category] = new[] { "category", "categoria", "type", "tipo", "group", "grupo", "familia", "rubro" },
        [CanonicalField.Unit] = new[] { "unit", "unidad", "uom", "measure", "medida", "presentacion" },
        [CanonicalField.Quantity] = new[] { "qty", "quantity", "cantidad", "cant", "units", "count", "amount", "existencia", "existencias", "stock" },
        [CanonicalField.UnitPrice] = new[] { "price", "unit price", "precio unitario", "precio", "cost", "costo", "unit cost", "pu", "valor", "importe" },
        [CanonicalField.Currency] = new[] { "currency", "moneda", "divisa" },
        [CanonicalField.Date] = new[] { "date", "fecha", "day", "dia", "time", "timestamp" },
        [CanonicalField.Location] = new[] { "location", "warehouse", "store", "branch", "almacen", "bodega", "ubicacion", "sucursal", "deposito", "tienda" },
        [CanonicalField.PartyName] = new[] { "supplier", "proveedor", "customer", "cliente", "vendor", "client", "party" },
        [CanonicalField.Direction] = new[] { "direction", "movement", "movimiento", "operacion", "tipo movimiento", "entrada salida", "in out" },
        [CanonicalField.Note] = new[] { "note", "notes", "nota", "notas", "comment", "comentario", "remark", "observacion", "observaciones" },
    };

    public MappingProposal Propose(RawTable table)
    {
        // Score every (field, column) pair, then assign globally by descending score so the most
        // confident matches win their column first (e.g. "Unit Price" -> UnitPrice, not Unit).
        var scored = new List<(CanonicalField Field, string Column, double Score)>();
        foreach (var info in CanonicalSchema.Fields)
        {
            var synonyms = Synonyms[info.Field];
            foreach (var col in table.Columns)
            {
                var header = HeaderProfile.Create(col.Name);
                double score = synonyms.Max(s => Match(header, s));

                score = ApplyDirectionSampleEvidence(info.Field, col, score);
                if (score <= 0) continue;

                score = Math.Clamp(score + TypeAdjustment(info.Field, col.InferredType), 0, 1.0);
                if (score >= Threshold) scored.Add((info.Field, col.Name, score));
            }
        }

        var assigned = new Dictionary<CanonicalField, (string Column, double Score)>();
        var usedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in scored.OrderByDescending(s => s.Score).ThenBy(s => (int)s.Field))
        {
            if (assigned.ContainsKey(s.Field) || usedColumns.Contains(s.Column)) continue;
            assigned[s.Field] = (s.Column, s.Score);
            usedColumns.Add(s.Column);
        }

        var fields = CanonicalSchema.Fields.Select(info =>
            assigned.TryGetValue(info.Field, out var a)
                ? new FieldMapping { Field = info.Field, SourceColumn = a.Column, Confidence = a.Score }
                : new FieldMapping { Field = info.Field, SourceColumn = null, Confidence = 0 }).ToList();

        return new MappingProposal
        {
            Fields = fields,
            DetectedRecordType = GuessRecordType(table, fields),
            Hints = GuessHints(table, fields),
            OverallConfidence = fields.Where(f => f.SourceColumn != null).Select(f => f.Confidence).DefaultIfEmpty(0).Average(),
            Rationale = "Heuristic header + type matching."
        };
    }

    private static double Match(HeaderProfile header, string rawSynonym)
    {
        var synonym = Normalize(rawSynonym);
        if (header.Normalized.Length == 0 || synonym.Length == 0) return 0;
        if (header.Normalized == synonym) return 1.0;

        var synonymTokens = synonym.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (InitialismMatches(header, synonymTokens)) return 0.93;

        var prefixScore = PrefixMatch(header, synonymTokens);
        if (prefixScore > 0) return prefixScore;

        var headerTokens = header.Tokens.Select(t => t.Text).ToArray();
        if (headerTokens.Contains(synonym)) return 0.85;                       // whole-token match
        // Substring matches only for synonyms long enough not to fire spuriously
        // (e.g. avoids "id" matching inside "cantidad").
        if (synonym.Length >= 4 && header.Normalized.Contains(synonym)) return 0.7;
        if (header.Normalized.Length >= 4 && synonym.Contains(header.Normalized)) return 0.6;
        return 0;
    }

    private static double PrefixMatch(HeaderProfile header, string[] synonymTokens)
    {
        if (synonymTokens.Length == 0) return 0;

        if (synonymTokens.Length == 1)
        {
            var synonymToken = synonymTokens[0];
            return header.Tokens.Any(t => t.Text != synonymToken && TokenPrefixMatches(t, synonymToken))
                ? 0.90
                : 0;
        }

        if (header.Tokens.Count != synonymTokens.Length) return 0;

        var abbreviated = false;
        for (var i = 0; i < synonymTokens.Length; i++)
        {
            var token = header.Tokens[i];
            if (!TokenPrefixMatches(token, synonymTokens[i])) return 0;
            abbreviated |= token.Text != synonymTokens[i];
        }

        return abbreviated ? 0.94 : 0;
    }

    private static bool TokenPrefixMatches(HeaderToken headerToken, string synonymToken)
    {
        if (headerToken.Text == synonymToken) return true;
        if (!synonymToken.StartsWith(headerToken.Text, StringComparison.Ordinal)) return false;
        return headerToken.Text.Length >= 3 || (headerToken.Text.Length >= 2 && headerToken.HadTrailingPeriod);
    }

    private static bool InitialismMatches(HeaderProfile header, string[] synonymTokens)
    {
        if (header.DottedInitialism is null || synonymTokens.Length < 2) return false;

        var initials = string.Concat(synonymTokens.Where(t => t.Length > 0).Select(t => t[0]));
        return header.DottedInitialism == initials;
    }

    private static double ApplyDirectionSampleEvidence(CanonicalField field, RawColumn column, double score)
    {
        var directionShare = DirectionSampleShare(column.SampleValues);
        if (directionShare < DirectionSampleDominanceThreshold) return score;

        return field switch
        {
            CanonicalField.Direction => Math.Max(score, DirectionSampleScore),
            CanonicalField.Category => Math.Min(score, CategoryDirectionSampleCeiling),
            _ => score
        };
    }

    private static double DirectionSampleShare(IReadOnlyList<string> samples)
    {
        var nonEmpty = samples
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
        if (nonEmpty.Count == 0) return 0;

        var directionValues = nonEmpty.Count(s => ValueProbe.ClassifyDirection(s) is not null);
        return (double)directionValues / nonEmpty.Count;
    }

    private static double TypeAdjustment(CanonicalField field, RawCellType type) => field switch
    {
        CanonicalField.Quantity when type is RawCellType.Number or RawCellType.Currency => 0.10,
        CanonicalField.Quantity when type is RawCellType.Text or RawCellType.Date or RawCellType.Boolean => -0.25,
        CanonicalField.UnitPrice when type is RawCellType.Currency or RawCellType.Number => 0.10,
        CanonicalField.UnitPrice when type is RawCellType.Text or RawCellType.Date or RawCellType.Boolean => -0.25,
        CanonicalField.Date when type is RawCellType.Date => 0.10,
        _ => 0
    };

    private static RecordType GuessRecordType(RawTable table, List<FieldMapping> fields)
    {
        if (fields.Any(f => f.Field == CanonicalField.Direction && f.SourceColumn != null))
            return RecordType.Mixed;

        var hay = Normalize($"{table.Meta.FileName} {table.Meta.SheetName}");
        if (MentionsAny(hay, "venta", "sale", "factura", "vendido")) return RecordType.Sales;
        if (MentionsAny(hay, "compra", "arrival", "ingreso", "recepcion", "purchase", "entrada")) return RecordType.Arrivals;
        if (MentionsAny(hay, "inventario", "stock", "conteo", "existencias", "count", "almacen")) return RecordType.StockCount;
        return RecordType.Unknown;
    }

    private static MappingHints GuessHints(RawTable table, List<FieldMapping> fields)
    {
        var hints = new MappingHints();

        var numericColumns = fields
            .Where(f => f.Field is CanonicalField.Quantity or CanonicalField.UnitPrice && f.SourceColumn != null)
            .Select(f => f.SourceColumn!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        int comma = 0, total = 0;
        foreach (var col in table.Columns.Where(c => numericColumns.Contains(c.Name)))
            foreach (var v in col.SampleValues)
                if (ValueProbe.LooksLikeNumber(v, out var isComma)) { total++; if (isComma) comma++; }
        hints.DecimalComma = total > 0 && comma * 2 > total;

        var priceColumn = fields.FirstOrDefault(f => f.Field == CanonicalField.UnitPrice)?.SourceColumn;
        if (priceColumn is not null)
        {
            var sample = table.Columns.First(c => c.Name == priceColumn).SampleValues;
            var detectedCurrency = sample.Select(ValueProbe.DetectCurrency).FirstOrDefault(c => c is not null);
            if (detectedCurrency is not null)
                hints.DefaultCurrency = detectedCurrency;
        }

        return hints;
    }

    /// <summary>Token-aware keyword test: matches whole words or word stems, so "venta" does NOT
    /// fire inside "inventario".</summary>
    private static bool MentionsAny(string haystack, params string[] needles)
    {
        var tokens = haystack.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return needles.Any(n => tokens.Any(t => t.StartsWith(n, StringComparison.Ordinal)));
    }

    private static string Normalize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed record HeaderProfile(string Normalized, IReadOnlyList<HeaderToken> Tokens, string? DottedInitialism)
    {
        public static HeaderProfile Create(string raw)
        {
            var tokens = new List<HeaderToken>();
            var token = new StringBuilder();

            void Flush(bool hadTrailingPeriod)
            {
                if (token.Length == 0) return;

                var normalized = Normalize(token.ToString());
                if (normalized.Length > 0)
                    tokens.Add(new HeaderToken(normalized, hadTrailingPeriod));
                token.Clear();
            }

            foreach (var ch in raw.Trim())
            {
                if (char.IsLetterOrDigit(ch))
                {
                    token.Append(ch);
                    continue;
                }

                Flush(ch == '.');
            }

            Flush(hadTrailingPeriod: false);

            var normalizedHeader = Normalize(raw);
            var dottedInitialism = tokens.Count >= 2 && tokens.All(t => t.HadTrailingPeriod && t.Text.Length == 1)
                ? string.Concat(tokens.Select(t => t.Text))
                : null;

            return new HeaderProfile(normalizedHeader, tokens, dottedInitialism);
        }
    }

    private sealed record HeaderToken(string Text, bool HadTrailingPeriod);
}
