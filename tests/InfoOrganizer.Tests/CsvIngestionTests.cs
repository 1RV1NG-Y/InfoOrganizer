using System.Text;
using InfoOrganizer.Domain;
using InfoOrganizer.Ingestion;

namespace InfoOrganizer.Tests;

public class CsvIngestionTests
{
    [Fact]
    public async Task Reads_bom_title_row_semicolon_delimiter_and_quoted_fields()
    {
        var text = "Reporte de inventario\r\nProducto;Cantidad;Precio;Nota\r\nCafe;2;12,50;\"con ; delimitador\"\r\n";
        var content = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray();
        var adapter = new CsvSourceAdapter();

        var table = (await adapter.ExtractAsync(new UploadedFile
        {
            FileName = "inventario.csv",
            Content = content,
            ContentType = "text/csv"
        })).Single();

        Assert.Equal(SourceType.Csv, table.Meta.SourceType);
        Assert.Equal(new[] { "Producto", "Cantidad", "Precio", "Nota" }, table.ColumnNames);
        Assert.Single(table.Rows);
        Assert.Equal("Cafe", table.Rows[0]["Producto"]);
        Assert.Equal("12,50", table.Rows[0]["Precio"]);
        Assert.Equal("con ; delimitador", table.Rows[0]["Nota"]);
    }
}
