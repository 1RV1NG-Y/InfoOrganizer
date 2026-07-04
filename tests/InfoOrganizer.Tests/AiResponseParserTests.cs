using InfoOrganizer.Ai;
using InfoOrganizer.Domain;

namespace InfoOrganizer.Tests;

public class AiResponseParserTests
{
    [Fact]
    public void ParseProposal_maps_columns_case_insensitively_and_clamps_confidence()
    {
        var table = new RawTable
        {
            Columns =
            [
                new RawColumn { Name = "Producto" },
                new RawColumn { Name = "Qty" }
            ]
        };

        const string json = """
        {
          "fields": [
            { "field": "ProductName", "sourceColumn": "producto", "confidence": 1.4 },
            { "field": "Quantity", "sourceColumn": "Qty", "confidence": 0.82 },
            { "field": "Sku", "sourceColumn": "Missing", "confidence": 0.5 },
            { "field": "UnknownField", "sourceColumn": "Producto", "confidence": 1 }
          ],
          "recordType": "Sales",
          "hints": {
            "dateFormat": "dd/MM/yyyy",
            "decimalComma": true,
            "defaultCurrency": "MXN"
          },
          "rationale": "Header and sample values match."
        }
        """;

        var proposal = AiResponseParser.ParseProposal(json, table);

        Assert.Equal("Producto", proposal.Column(CanonicalField.ProductName));
        Assert.Equal("Qty", proposal.Column(CanonicalField.Quantity));
        Assert.Null(proposal.Column(CanonicalField.Sku));
        Assert.Equal(RecordType.Sales, proposal.DetectedRecordType);
        Assert.Equal("dd/MM/yyyy", proposal.Hints.DateFormat);
        Assert.True(proposal.Hints.DecimalComma);
        Assert.Equal("MXN", proposal.Hints.DefaultCurrency);
        Assert.Equal(0.91, proposal.OverallConfidence, precision: 2);
        Assert.Equal("Header and sample values match.", proposal.Rationale);
    }

    [Fact]
    public void ParseImageTable_normalizes_headers_rows_and_metadata()
    {
        const string json = """
        {
          "columns": [" Producto ", "", "Producto"],
          "rows": [
            ["Manzana", "10", "A"],
            ["", "", ""],
            ["Pera"]
          ],
          "notes": "Second line was blank."
        }
        """;

        var table = AiResponseParser.ParseImageTable(json, "foto.jpg");

        Assert.Equal(SourceType.Image, table.Meta.SourceType);
        Assert.Equal("foto.jpg", table.Meta.FileName);
        Assert.Equal("Second line was blank.", table.Meta.Notes);
        Assert.Equal(["Producto", "Column1", "Producto (2)"], table.ColumnNames);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("Manzana", table.Rows[0]["Producto"]);
        Assert.Equal("10", table.Rows[0]["Column1"]);
        Assert.Equal("A", table.Rows[0]["Producto (2)"]);
        Assert.Equal("Pera", table.Rows[1]["Producto"]);
        Assert.Null(table.Rows[1]["Column1"]);
        Assert.Null(table.Rows[1]["Producto (2)"]);
    }
}
