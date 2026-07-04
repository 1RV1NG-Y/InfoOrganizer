using ClosedXML.Excel;
using InfoOrganizer.Domain;
using InfoOrganizer.Ingestion;

namespace InfoOrganizer.Tests;

public class ExcelIngestionTests
{
    private static byte[] BuildMessyWorkbook()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Inventario");

        // Title banner above the real header (must be stepped over).
        ws.Cell(1, 1).Value = "Inventario Marzo 2024";

        ws.Cell(2, 1).Value = "Producto";
        ws.Cell(2, 2).Value = "Cantidad";
        ws.Cell(2, 3).Value = "Precio";
        ws.Cell(2, 4).Value = "Fecha";

        var rows = new (string name, int qty, double price, DateTime date)[]
        {
            ("Manzana", 100, 0.50, new DateTime(2024, 3, 1)),
            ("Pera", 40, 0.75, new DateTime(2024, 3, 2)),
            ("Platano", 12, 0.30, new DateTime(2024, 3, 5)),
        };

        int r = 3;
        foreach (var row in rows)
        {
            ws.Cell(r, 1).Value = row.name;
            ws.Cell(r, 2).Value = row.qty;
            ws.Cell(r, 3).Value = row.price;
            ws.Cell(r, 3).Style.NumberFormat.Format = "$#,##0.00";
            ws.Cell(r, 4).Value = row.date;
            ws.Cell(r, 4).Style.DateFormat.Format = "yyyy-MM-dd";
            r++;
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    [Fact]
    public async Task Extract_finds_header_below_title_and_reads_rows()
    {
        var adapter = new ExcelSourceAdapter();
        var file = new UploadedFile { FileName = "inv.xlsx", Content = BuildMessyWorkbook() };

        var tables = await adapter.ExtractAsync(file);
        var table = Assert.Single(tables);

        Assert.Equal(new[] { "Producto", "Cantidad", "Precio", "Fecha" }, table.ColumnNames);
        Assert.Equal(3, table.Rows.Count);
        Assert.Equal("Manzana", table.Rows[0]["Producto"]);
        Assert.Equal("Inventario", table.Meta.SheetName);
        Assert.Equal(SourceType.Excel, table.Meta.SourceType);
    }

    [Fact]
    public async Task Profiler_infers_column_types()
    {
        var adapter = new ExcelSourceAdapter();
        var file = new UploadedFile { FileName = "inv.xlsx", Content = BuildMessyWorkbook() };
        var table = (await adapter.ExtractAsync(file)).Single();

        new ColumnProfiler().Profile(table);

        RawCellType TypeOf(string col) => table.Columns.First(c => c.Name == col).InferredType;
        Assert.Equal(RawCellType.Text, TypeOf("Producto"));
        Assert.Equal(RawCellType.Number, TypeOf("Cantidad"));
        Assert.Equal(RawCellType.Currency, TypeOf("Precio"));
        Assert.Equal(RawCellType.Date, TypeOf("Fecha"));
    }
}
