using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace InfoOrganizer.Mapping;

/// <summary>A stable hash of a table's column set. Order-, case-, whitespace-, and accent-insensitive so
/// the same real-world format always yields the same fingerprint — the key to auto-reusing a saved mapping.</summary>
public static class SchemaFingerprint
{
    private const char Separator = '';

    public static string Compute(IEnumerable<string> columnNames)
    {
        var normalized = columnNames
            .Select(Normalize)
            .Where(s => s.Length > 0)
            .Distinct()
            .OrderBy(s => s, StringComparer.Ordinal);

        var joined = string.Join(Separator, normalized);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(joined));
        return Convert.ToHexStringLower(hash);
    }

    private static string Normalize(string s)
    {
        var lowered = s.Trim().ToLowerInvariant();
        var sb = new StringBuilder(lowered.Length);
        foreach (var ch in lowered.Normalize(NormalizationForm.FormD))
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark) continue; // drop accents
            if (char.IsWhiteSpace(ch)) continue;
            sb.Append(ch);
        }
        return sb.ToString();
    }
}
