using InfoOrganizer.Domain;
using System.Text.Json.Serialization;

namespace InfoOrganizer.Evals.EvalEngine;

public sealed class ExpectedOutcome
{
    public RecordType RecordType { get; set; }
    public Dictionary<string, string?> Mapping { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<StockExpectation> Stock { get; set; } = new();
    public List<LocationStockExpectation> StockByLocation { get; set; } = new();
    public List<int> FlaggedRowIndexes { get; set; } = new();
    public int? AppliedRows { get; set; }
    public int? StagedRows { get; set; }
    public string? DefaultCurrency { get; set; }
    public string? KnownIssue { get; set; }

    public void Normalize()
    {
        Mapping = new Dictionary<string, string?>(Mapping, StringComparer.OrdinalIgnoreCase);
        FlaggedRowIndexes = FlaggedRowIndexes.Distinct().Order().ToList();
    }
}

public sealed class StockExpectation
{
    public string? Sku { get; set; }
    public string? ProductName { get; set; }
    public decimal ExpectedOnHand { get; set; }

    [JsonIgnore]
    public string Key => !string.IsNullOrWhiteSpace(Sku) ? $"sku:{Sku}" : $"name:{ProductName}";
}

public sealed class LocationStockExpectation
{
    public string Product { get; set; } = "";
    public string Location { get; set; } = "";
    public decimal ExpectedOnHand { get; set; }
}
