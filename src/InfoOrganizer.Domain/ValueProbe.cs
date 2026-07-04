using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace InfoOrganizer.Domain;

/// <summary>Locale-tolerant detection and parsing of raw cell text. Shared by the column profiler
/// (type detection) and the normalizer (value parsing with confirmed hints). Origin-agnostic: it
/// works the same on cells from Excel and from photo extraction.</summary>
public static partial class ValueProbe
{
    private static readonly string[] CurrencySymbols = { "$", "€", "£", "¥", "₹", "₡", "₩", "R$", "kr" };
    private static readonly string[] CurrencyCodes =
        { "USD", "EUR", "GBP", "MXN", "CAD", "AUD", "JPY", "INR", "BRL", "CNY", "CHF", "COP", "ARS", "CLP", "PEN" };

    private static readonly HashSet<string> TrueWords = new(StringComparer.OrdinalIgnoreCase)
        { "true", "yes", "y", "sí", "si", "verdadero", "x", "✓" };
    private static readonly HashSet<string> FalseWords = new(StringComparer.OrdinalIgnoreCase)
        { "false", "no", "n", "falso" };

    private static readonly string[] InWords =
        { "in", "ingreso", "entrada", "entradas", "compra", "compras", "purchase", "received", "receipt",
          "recibido", "restock", "arrival", "arrivals", "buy", "stock in", "ingresos" };
    private static readonly string[] OutWords =
        { "out", "salida", "salidas", "venta", "ventas", "sale", "sales", "sold", "vendido", "despacho",
          "sell", "stock out", "shipment", "egreso" };
    private static readonly string[] AdjustmentWords =
        { "ajuste", "adjust", "adjustment", "correccion", "correction", "merma", "dano", "danado",
          "damage", "damaged", "shrink", "shrinkage", "waste", "conteo", "recuento" };

    [GeneratedRegex(@"^-?\d{1,3}([.,]\d{3})*([.,]\d+)?$|^-?\d+([.,]\d+)?$")]
    private static partial Regex NumberLike();

    // ---- Detection (used by the profiler) ----

    public static RawCellType InferType(IReadOnlyList<string> values)
    {
        if (values.Count == 0) return RawCellType.Unknown;

        int currency = 0, number = 0, date = 0, boolean = 0;
        foreach (var v in values)
        {
            if (LooksLikeBoolean(v)) boolean++;
            else if (LooksLikeDate(v)) date++;
            else if (LooksLikeCurrency(v)) currency++;
            else if (LooksLikeNumber(v, out _)) number++;
        }

        double n = values.Count;
        // Currency and number share the numeric family; prefer the more specific currency.
        if ((currency + number) / n >= 0.6 && currency >= number) return RawCellType.Currency;
        if ((currency + number) / n >= 0.6) return RawCellType.Number;
        if (date / n >= 0.6) return RawCellType.Date;
        if (boolean / n >= 0.6) return RawCellType.Boolean;
        return RawCellType.Text;
    }

    public static bool LooksLikeBoolean(string s)
    {
        s = s.Trim();
        return TrueWords.Contains(s) || FalseWords.Contains(s);
    }

    public static bool LooksLikeCurrency(string s)
    {
        var t = s.Trim();
        bool hasMarker = CurrencySymbols.Any(t.Contains)
            || CurrencyCodes.Any(c => t.Contains(c, StringComparison.OrdinalIgnoreCase));
        return hasMarker && LooksLikeNumber(StripCurrency(t), out _);
    }

    public static bool LooksLikeNumber(string s, out bool commaDecimal)
    {
        commaDecimal = false;
        var t = StripCurrency(s).Trim();
        if (t.Length == 0) return false;
        if (!NumberLike().IsMatch(t)) return false;

        int lastDot = t.LastIndexOf('.');
        int lastComma = t.LastIndexOf(',');
        commaDecimal = lastComma > lastDot;
        return true;
    }

    public static bool LooksLikeDate(string s) => TryParseDate(s, null, out _);

    // ---- Parsing (used by the normalizer, with confirmed hints) ----

    private static readonly string[] DateFormats =
    {
        "yyyy-MM-dd", "yyyy/MM/dd", "dd/MM/yyyy", "MM/dd/yyyy", "d/M/yyyy", "M/d/yyyy",
        "dd-MM-yyyy", "MM-dd-yyyy", "dd.MM.yyyy", "d-MMM-yyyy", "dd MMM yyyy", "MMM d, yyyy",
        "dd/MM/yy", "MM/dd/yy", "yyyyMMdd"
    };

    public static bool TryParseDate(string? s, string? format, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim();

        if (!string.IsNullOrWhiteSpace(format) &&
            DateTime.TryParseExact(s, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
        {
            date = DateOnly.FromDateTime(exact);
            return true;
        }

        if (DateTime.TryParseExact(s, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            date = DateOnly.FromDateTime(dt);
            return true;
        }

        foreach (var culture in new[] { CultureInfo.InvariantCulture, new CultureInfo("en-US"), new CultureInfo("es-ES") })
        {
            if (DateTime.TryParse(s, culture, DateTimeStyles.None, out var parsed))
            {
                date = DateOnly.FromDateTime(parsed);
                return true;
            }
        }
        return false;
    }

    public static bool TryParseDecimal(string? s, bool commaDecimal, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        var t = StripCurrency(s).Trim().Replace(" ", "").Replace(" ", "");
        if (t.Length == 0) return false;

        // Normalize to invariant: drop the thousands separator, make '.' the decimal point.
        t = commaDecimal
            ? t.Replace(".", "").Replace(",", ".")
            : t.Replace(",", "");

        return decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    public static string? DetectCurrency(string s)
    {
        var code = CurrencyCodes.FirstOrDefault(c => s.Contains(c, StringComparison.OrdinalIgnoreCase));
        if (code is not null) return code.ToUpperInvariant();
        // "$" is ambiguous across many local currencies; let the confirmed/default currency decide.
        if (s.Contains('€')) return "EUR";
        if (s.Contains('£')) return "GBP";
        if (s.Contains('¥')) return "JPY";
        if (s.Contains('₹')) return "INR";
        return null;
    }

    /// <summary>Interpret a per-row direction cell as In, Out, or Adjustment. Null when undecidable.</summary>
    public static MovementKind? ClassifyDirection(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var raw = s.Trim();
        if (raw is "+") return MovementKind.In;
        if (raw is "-") return MovementKind.Out;
        var t = NormalizeForKeywordMatch(raw);
        if (AdjustmentWords.Any(w => t.Contains(w))) return MovementKind.Adjustment;
        if (InWords.Any(w => t.Contains(w))) return MovementKind.In;
        if (OutWords.Any(w => t.Contains(w))) return MovementKind.Out;
        return null;
    }

    private static string NormalizeForKeywordMatch(string s)
    {
        var sb = new StringBuilder(s.Trim().Length);
        foreach (var ch in s.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }

        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string StripCurrency(string s)
    {
        var t = s;
        foreach (var sym in CurrencySymbols) t = t.Replace(sym, "");
        foreach (var code in CurrencyCodes)
            t = Regex.Replace(t, Regex.Escape(code), "", RegexOptions.IgnoreCase);
        return t.Trim();
    }
}
