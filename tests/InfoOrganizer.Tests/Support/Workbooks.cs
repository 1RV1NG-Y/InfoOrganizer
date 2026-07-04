using ClosedXML.Excel;

namespace InfoOrganizer.Tests.Support;

public static class Workbooks
{
    /// <summary>Spanish arrivals: Producto / Cantidad / Precio / Fecha.</summary>
    public static byte[] ArrivalsEs()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Compras");
        ws.Cell(1, 1).Value = "Producto";
        ws.Cell(1, 2).Value = "Cantidad";
        ws.Cell(1, 3).Value = "Precio";
        ws.Cell(1, 4).Value = "Fecha";

        var data = new (string Name, int Qty, double Price, DateTime Date)[]
        {
            ("Manzana", 100, 0.50, new DateTime(2024, 3, 1)),
            ("Pera", 40, 0.75, new DateTime(2024, 3, 2)),
            ("Platano", 12, 0.30, new DateTime(2024, 3, 5)),
        };

        int r = 2;
        foreach (var d in data)
        {
            ws.Cell(r, 1).Value = d.Name;
            ws.Cell(r, 2).Value = d.Qty;
            ws.Cell(r, 3).Value = d.Price;
            ws.Cell(r, 3).Style.NumberFormat.Format = "$#,##0.00";
            ws.Cell(r, 4).Value = d.Date;
            ws.Cell(r, 4).Style.DateFormat.Format = "yyyy-MM-dd";
            r++;
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    /// <summary>A stock count: Producto / Cantidad (absolute on-hand).</summary>
    public static byte[] StockCountEs(string product, int qty)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Conteo");
        ws.Cell(1, 1).Value = "Producto";
        ws.Cell(1, 2).Value = "Cantidad";
        ws.Cell(2, 1).Value = product;
        ws.Cell(2, 2).Value = qty;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
