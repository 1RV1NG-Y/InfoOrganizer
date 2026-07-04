using ClosedXML.Excel;
using InfoOrganizer.Domain;
using InfoOrganizer.Evals.EvalEngine;

namespace InfoOrganizer.Evals.Generation;

public static class FixtureGenerator
{
    private static readonly string[] MixedHeaders =
    {
        "Fecha", "Tipo Movimiento", "Código", "Producto", "Cantidad", "Unidad",
        "Almacén", "Proveedor/Cliente", "Precio Unitario", "Nota"
    };

    public static void Generate(string repositoryRoot)
    {
        var evalsRoot = Path.Combine(repositoryRoot, "evals");
        var fixturesRoot = Path.Combine(evalsRoot, "fixtures");
        var safeRoot = Path.GetFullPath(evalsRoot);
        var safeFixtures = Path.GetFullPath(fixturesRoot);
        if (!safeFixtures.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Refusing to rewrite unexpected path {safeFixtures}.");

        if (Directory.Exists(fixturesRoot))
            Directory.Delete(fixturesRoot, recursive: true);
        Directory.CreateDirectory(fixturesRoot);

        WriteReadme(evalsRoot);
        CleanMixed(fixturesRoot);
        TitleBanner(fixturesRoot);
        SemicolonCommaDecimals(fixturesRoot);
        AbbreviatedHeaders(fixturesRoot);
        ArrivalsOnly(fixturesRoot);
        SalesOnly(fixturesRoot);
        StockCount(fixturesRoot);
        AdjustmentWords(fixturesRoot);
        BrokenRows(fixturesRoot);
        BadDatesPrices(fixturesRoot);
        DuplicateHeaders(fixturesRoot);
        ExtraColumns(fixturesRoot);
        EnglishHeaders(fixturesRoot);
        BomQuotedCsv(fixturesRoot);
        NoLocation(fixturesRoot);
    }

    private static void CleanMixed(string root)
    {
        var rows = new[]
        {
            Row("2024-01-02", "Entrada", "CAF-250", "Cafe molido 250g", "20", "pza", "Bodega", "Tostadora Norte", "68.50", "Compra inicial"),
            Row("2024-01-03", "Entrada", "AZU-001", "Azucar 1kg", "15", "kg", "Bodega", "Dulces MX", "22.00", "Compra"),
            Row("2024-01-04", "Venta", "CAF-250", "Cafe molido 250g", "4", "pza", "Tienda", "Cliente mostrador", "95.00", "Venta mostrador"),
            Row("2024-01-05", "Salida", "AZU-001", "Azucar 1kg", "3", "kg", "Tienda", "Merma", "0.00", "Empaque roto"),
            Row("2024-01-06", "Ajuste(-1)", "CAF-250", "Cafe molido 250g", "-1", "pza", "Bodega", "Inventario", "0.00", "Correccion"),
            Row("2024-01-07", "Entrada", "VAS-012", "Vasos 12oz", "200", "pza", "Bodega", "Plasticos Centro", "1.20", "Compra"),
            Row("2024-01-08", "Venta", "VAS-012", "Vasos 12oz", "35", "pza", "Tienda", "Cliente evento", "2.00", "Venta"),
            Row("2024-01-09", "Entrada", "CAF-250", "Cafe molido 250g", "7", "pza", "Bodega", "Tostadora Norte", "67.00", "Reposicion"),
            Row("2024-01-10", "Venta", "AZU-001", "Azucar 1kg", "2", "kg", "Tienda", "Cliente cafe", "28.00", "Venta"),
            Row("2024-01-11", "Ajuste(-1)", "VAS-012", "Vasos 12oz", "-10", "pza", "Bodega", "Conteo", "0.00", "Danados")
        };

        var dir = FixtureDir(root, "clean-mixed-xlsx-csv");
        WriteXlsx(Path.Combine(dir, "source.xlsx"), "Movimientos", MixedHeaders, rows);
        WriteCsv(Path.Combine(dir, "source.csv"), MixedHeaders, rows);
        WriteExpected(dir, ExpectedMixed(
            stock: new[] { Stock("CAF-250", 22m), Stock("AZU-001", 10m), Stock("VAS-012", 155m) },
            locations: new[]
            {
                Loc("CAF-250", "Bodega", 26m), Loc("CAF-250", "Tienda", -4m),
                Loc("AZU-001", "Bodega", 15m), Loc("AZU-001", "Tienda", -5m),
                Loc("VAS-012", "Bodega", 190m), Loc("VAS-012", "Tienda", -35m)
            },
            stagedRows: 10,
            appliedRows: 10,
            defaultCurrency: "MXN"));
    }

    private static void TitleBanner(string root)
    {
        var rows = new[]
        {
            Row("2024-02-01", "Entrada", "MIE-100", "Miel 370g", "12", "frasco", "Bodega", "Apicola Sur", "55.00", "Compra banner"),
            Row("2024-02-02", "Venta", "MIE-100", "Miel 370g", "3", "frasco", "Tienda", "Cliente caja", "75.00", "Venta"),
            Row("2024-02-03", "Entrada", "CAF-250", "Cafe molido 250g", "8", "pza", "Bodega", "Tostadora Norte", "68.00", "Compra"),
            Row("2024-02-04", "Ajuste(-1)", "MIE-100", "Miel 370g", "-2", "frasco", "Bodega", "Conteo", "0.00", "Frascos rotos")
        };

        var dir = FixtureDir(root, "title-banner");
        WriteTitleBannerXlsx(Path.Combine(dir, "source.xlsx"), MixedHeaders, rows);
        WriteExpected(dir, ExpectedMixed(
            stock: new[] { Stock("MIE-100", 7m), Stock("CAF-250", 8m) },
            locations: new[]
            {
                Loc("MIE-100", "Bodega", 10m), Loc("MIE-100", "Tienda", -3m),
                Loc("CAF-250", "Bodega", 8m)
            },
            stagedRows: 4,
            appliedRows: 4,
            defaultCurrency: "MXN"));
    }

    private static void SemicolonCommaDecimals(string root)
    {
        var rows = new[]
        {
            Row("2024-03-01", "Entrada", "ACE-1L", "Aceite 1L", "10,5", "lt", "Bodega", "Oleicos Norte", "1.234,56", "Compra"),
            Row("2024-03-02", "Venta", "ACE-1L", "Aceite 1L", "2,5", "lt", "Tienda", "Cliente mostrador", "1.500,00", "Venta"),
            Row("2024-03-03", "Ajuste(-1)", "ACE-1L", "Aceite 1L", "-1,0", "lt", "Bodega", "Conteo", "0,00", "Derrame"),
            Row("2024-03-04", "Entrada", "HAR-1K", "Harina 1kg", "3,0", "kg", "Bodega", "Molino Centro", "25,50", "Compra")
        };

        var dir = FixtureDir(root, "semicolon-comma-decimals");
        WriteCsv(Path.Combine(dir, "source.csv"), MixedHeaders, rows, delimiter: ';');
        WriteExpected(dir, ExpectedMixed(
            stock: new[] { Stock("ACE-1L", 7.0m), Stock("HAR-1K", 3.0m) },
            locations: new[]
            {
                Loc("ACE-1L", "Bodega", 9.5m), Loc("ACE-1L", "Tienda", -2.5m),
                Loc("HAR-1K", "Bodega", 3.0m)
            },
            stagedRows: 4,
            appliedRows: 4,
            defaultCurrency: "MXN"));
    }

    private static void AbbreviatedHeaders(string root)
    {
        var headers = new[] { "Fec.", "Tipo Mov.", "Cód.", "Producto", "Cant.", "Unid.", "Suc.", "Prov.", "P.U.", "Nota" };
        var rows = new[]
        {
            Row("2024-04-01", "Entrada", "BOL-01", "Bolsa compostable", "30", "pza", "Centro", "Eco Pack", "1.50", "Compra"),
            Row("2024-04-02", "Venta", "BOL-01", "Bolsa compostable", "8", "pza", "Centro", "Cliente caja", "2.50", "Venta"),
            Row("2024-04-03", "Ajuste(-1)", "BOL-01", "Bolsa compostable", "-2", "pza", "Centro", "Conteo", "0.00", "Rotas"),
            Row("2024-04-04", "Entrada", "CAJ-02", "Caja chica", "12", "pza", "Centro", "Papeles SA", "6.00", "Compra")
        };

        var dir = FixtureDir(root, "abbreviated-headers");
        WriteXlsx(Path.Combine(dir, "source.xlsx"), "Movimientos", headers, rows);
        WriteExpected(dir, new ExpectedOutcome
        {
            RecordType = RecordType.Mixed,
            Mapping = Mapping(
                (CanonicalField.Date, "Fec."),
                (CanonicalField.Direction, "Tipo Mov."),
                (CanonicalField.Sku, "Cód."),
                (CanonicalField.ProductName, "Producto"),
                (CanonicalField.Quantity, "Cant."),
                (CanonicalField.Unit, "Unid."),
                (CanonicalField.Location, "Suc."),
                (CanonicalField.PartyName, "Prov."),
                (CanonicalField.UnitPrice, "P.U."),
                (CanonicalField.Note, "Nota")),
            Stock = new() { Stock("BOL-01", 20m), Stock("CAJ-02", 12m) },
            StagedRows = 4,
            AppliedRows = 4
        });
    }

    private static void ArrivalsOnly(string root)
    {
        var headers = new[] { "Fecha", "Código", "Producto", "Cantidad", "Unidad", "Almacén", "Proveedor", "Precio Unitario", "Nota" };
        var rows = new[]
        {
            Row("2024-05-01", "CAF-250", "Cafe molido 250g", "10", "pza", "Bodega", "Tostadora Norte", "68.50", "Compra enero"),
            Row("2024-05-02", "AZU-001", "Azucar 1kg", "20", "kg", "Bodega", "Dulces MX", "22.00", "Compra enero"),
            Row("2024-05-03", "VAS-012", "Vasos 12oz", "100", "pza", "Bodega", "Plasticos Centro", "1.20", "Compra enero")
        };

        var dir = FixtureDir(root, "arrivals-only");
        WriteXlsx(Path.Combine(dir, "source.xlsx"), "Compras enero", headers, rows);
        WriteExpected(dir, ExpectedNoDirection(
            RecordType.Arrivals,
            headers,
            partyColumn: "Proveedor",
            stock: new[] { Stock("CAF-250", 10m), Stock("AZU-001", 20m), Stock("VAS-012", 100m) },
            locations: new[] { Loc("CAF-250", "Bodega", 10m), Loc("AZU-001", "Bodega", 20m), Loc("VAS-012", "Bodega", 100m) },
            stagedRows: 3,
            appliedRows: 3,
            defaultCurrency: "MXN"));
    }

    private static void SalesOnly(string root)
    {
        var headers = new[] { "Fecha", "Código", "Producto", "Cantidad", "Unidad", "Almacén", "Cliente", "Precio Unitario", "Nota" };
        var rows = new[]
        {
            Row("2024-06-01", "CAF-250", "Cafe molido 250g", "6", "pza", "Tienda", "Cliente mostrador", "95.00", "Venta mostrador"),
            Row("2024-06-01", "AZU-001", "Azucar 1kg", "4", "kg", "Tienda", "Cliente mostrador", "28.00", "Venta mostrador"),
            Row("2024-06-02", "VAS-012", "Vasos 12oz", "30", "pza", "Tienda", "Cliente evento", "2.00", "Venta mostrador")
        };

        var dir = FixtureDir(root, "sales-only");
        WriteXlsx(Path.Combine(dir, "source.xlsx"), "Ventas mostrador", headers, rows);
        WriteExpected(dir, ExpectedNoDirection(
            RecordType.Sales,
            headers,
            partyColumn: "Cliente",
            stock: new[] { Stock("CAF-250", -6m), Stock("AZU-001", -4m), Stock("VAS-012", -30m) },
            locations: new[] { Loc("CAF-250", "Tienda", -6m), Loc("AZU-001", "Tienda", -4m), Loc("VAS-012", "Tienda", -30m) },
            stagedRows: 3,
            appliedRows: 3,
            defaultCurrency: "MXN"));
    }

    private static void StockCount(string root)
    {
        var headers = new[] { "Código", "Producto", "Cantidad", "Unidad", "Almacén", "Nota" };
        var rows = new[]
        {
            Row("CAF-250", "Cafe molido 250g", "18", "pza", "Bodega", "Conteo fisico"),
            Row("AZU-001", "Azucar 1kg", "6", "kg", "Bodega", "Conteo fisico")
        };

        var dir = FixtureDir(root, "stock-count");
        WriteXlsx(Path.Combine(dir, "source.xlsx"), "Conteo físico", headers, rows);
        WriteExpected(dir, new ExpectedOutcome
        {
            RecordType = RecordType.StockCount,
            Mapping = Mapping(
                (CanonicalField.Sku, "Código"),
                (CanonicalField.ProductName, "Producto"),
                (CanonicalField.Quantity, "Cantidad"),
                (CanonicalField.Unit, "Unidad"),
                (CanonicalField.Location, "Almacén"),
                (CanonicalField.Note, "Nota")),
            Stock = new() { Stock("CAF-250", 18m), Stock("AZU-001", 6m) },
            StockByLocation = new() { Loc("CAF-250", "Bodega", 18m), Loc("AZU-001", "Bodega", 6m) },
            StagedRows = 2,
            AppliedRows = 2
        });
    }

    private static void AdjustmentWords(string root)
    {
        var rows = new[]
        {
            Row("2024-07-01", "Entrada", "DET-500", "Detergente 500ml", "50", "pza", "Bodega", "Limpios SA", "20.00", "Compra"),
            Row("2024-07-02", "merma", "DET-500", "Detergente 500ml", "-3", "pza", "Bodega", "Inventario", "0.00", "Fuga"),
            Row("2024-07-03", "corrección", "DET-500", "Detergente 500ml", "2", "pza", "Bodega", "Conteo", "0.00", "Sobrante"),
            Row("2024-07-04", "dañado", "DET-500", "Detergente 500ml", "-1", "pza", "Tienda", "Inventario", "0.00", "Envase roto"),
            Row("2024-07-05", "Venta", "DET-500", "Detergente 500ml", "10", "pza", "Tienda", "Cliente mostrador", "28.00", "Venta")
        };

        var dir = FixtureDir(root, "adjustment-words");
        WriteXlsx(Path.Combine(dir, "source.xlsx"), "Movimientos", MixedHeaders, rows);
        WriteExpected(dir, ExpectedMixed(
            stock: new[] { Stock("DET-500", 38m) },
            locations: new[] { Loc("DET-500", "Bodega", 49m), Loc("DET-500", "Tienda", -11m) },
            stagedRows: 5,
            appliedRows: 5,
            defaultCurrency: "MXN"));
    }

    private static void BrokenRows(string root)
    {
        var rows = new[]
        {
            Row("2024-08-01", "Entrada", "CAF-250", "Cafe molido 250g", "5", "pza", "Bodega", "Tostadora Norte", "68.50", "Valida"),
            Row("2024-08-02", "Entrada", "AZU-001", "Azucar 1kg", "", "kg", "Bodega", "Dulces MX", "22.00", "Falta cantidad"),
            Row("2024-08-03", "Venta", "", "", "2", "pza", "Tienda", "Cliente mostrador", "10.00", "Falta producto"),
            Row("2024-08-04", "Venta", "CAF-250", "Cafe molido 250g", "1", "pza", "Tienda", "Cliente mostrador", "95.00", "Valida"),
            Row("2024-08-05", "Ajuste(-1)", "AZU-001", "Azucar 1kg", "4", "kg", "Bodega", "Conteo", "0.00", "Valida")
        };

        var dir = FixtureDir(root, "broken-rows");
        WriteXlsx(Path.Combine(dir, "source.xlsx"), "Movimientos", MixedHeaders, rows);
        var expected = ExpectedMixed(
            stock: new[] { Stock("CAF-250", 4m), Stock("AZU-001", 4m) },
            locations: Array.Empty<LocationStockExpectation>(),
            stagedRows: 5,
            appliedRows: 3,
            defaultCurrency: "MXN");
        expected.FlaggedRowIndexes = new() { 1, 2 };
        WriteExpected(dir, expected);
    }

    private static void BadDatesPrices(string root)
    {
        var rows = new[]
        {
            Row("2024-09-01", "Entrada", "GAL-01", "Galleta avena", "10", "pza", "Bodega", "Panificadora", "12.50", "Valida"),
            Row("no-es-fecha", "Venta", "GAL-01", "Galleta avena", "2", "pza", "Tienda", "Cliente", "18.00", "Fecha mala"),
            Row("2024-09-03", "Entrada", "JUG-01", "Jugo mango", "5", "pza", "Bodega", "Bebidas SA", "gratis?", "Precio malo"),
            Row("2024-09-04", "Venta", "JUG-01", "Jugo mango", "1", "pza", "Tienda", "Cliente", "20.00", "Valida")
        };

        var dir = FixtureDir(root, "bad-dates-prices");
        WriteXlsx(Path.Combine(dir, "source.xlsx"), "Movimientos", MixedHeaders, rows);
        WriteExpected(dir, ExpectedMixed(
            stock: new[] { Stock("GAL-01", 8m), Stock("JUG-01", 4m) },
            locations: new[]
            {
                Loc("GAL-01", "Bodega", 10m), Loc("GAL-01", "Tienda", -2m),
                Loc("JUG-01", "Bodega", 5m), Loc("JUG-01", "Tienda", -1m)
            },
            stagedRows: 4,
            appliedRows: 4,
            defaultCurrency: "MXN"));
    }

    private static void DuplicateHeaders(string root)
    {
        var headers = new[] { "Fecha", "Tipo Movimiento", "Código", "Producto", "Cantidad", "Cantidad", "Almacén", "Nota" };
        var rows = new[]
        {
            Row("2024-10-01", "Entrada", "DUP-1", "Producto duplicado", "caja", "10", "Bodega", "Compra"),
            Row("2024-10-02", "Venta", "DUP-1", "Producto duplicado", "pieza", "3", "Tienda", "Venta"),
            Row("2024-10-03", "Entrada", "DUP-2", "Otro duplicado", "caja", "4", "Bodega", "Compra")
        };

        var dir = FixtureDir(root, "duplicate-headers");
        WriteXlsx(Path.Combine(dir, "source.xlsx"), "Movimientos", headers, rows);
        WriteExpected(dir, new ExpectedOutcome
        {
            RecordType = RecordType.Mixed,
            Mapping = Mapping(
                (CanonicalField.Date, "Fecha"),
                (CanonicalField.Direction, "Tipo Movimiento"),
                (CanonicalField.Sku, "Código"),
                (CanonicalField.ProductName, "Producto"),
                (CanonicalField.Quantity, "Cantidad (2)"),
                (CanonicalField.Location, "Almacén"),
                (CanonicalField.Note, "Nota")),
            Stock = new() { Stock("DUP-1", 7m), Stock("DUP-2", 4m) },
            StockByLocation = new()
            {
                Loc("DUP-1", "Bodega", 10m), Loc("DUP-1", "Tienda", -3m),
                Loc("DUP-2", "Bodega", 4m)
            },
            StagedRows = 3,
            AppliedRows = 3
        });
    }

    private static void ExtraColumns(string root)
    {
        var headers = new[]
        {
            "Fecha", "Tipo Movimiento", "Código", "Producto", "Cantidad", "Unidad",
            "Almacén", "Proveedor/Cliente", "Precio Unitario", "Nota", "Color", "Estante", "Lote"
        };
        var rows = new[]
        {
            Row("2024-11-01", "Entrada", "EXT-1", "Etiqueta precio", "40", "pza", "Bodega", "Papeles SA", "0.50", "Compra", "Rojo", "A1", "L-100"),
            Row("2024-11-02", "Venta", "EXT-1", "Etiqueta precio", "12", "pza", "Tienda", "Cliente", "1.00", "Venta", "Rojo", "Caja", "L-100"),
            Row("2024-11-03", "Entrada", "EXT-2", "Cinta kraft", "6", "rollo", "Bodega", "Papeles SA", "18.00", "Compra", "Cafe", "B2", "L-200")
        };

        var dir = FixtureDir(root, "extra-columns");
        WriteXlsx(Path.Combine(dir, "source.xlsx"), "Movimientos", headers, rows);
        WriteExpected(dir, new ExpectedOutcome
        {
            RecordType = RecordType.Mixed,
            Mapping = Mapping(
                (CanonicalField.Date, "Fecha"),
                (CanonicalField.Direction, "Tipo Movimiento"),
                (CanonicalField.Sku, "Código"),
                (CanonicalField.ProductName, "Producto"),
                (CanonicalField.Quantity, "Cantidad"),
                (CanonicalField.Unit, "Unidad"),
                (CanonicalField.Location, "Almacén"),
                (CanonicalField.PartyName, "Proveedor/Cliente"),
                (CanonicalField.UnitPrice, "Precio Unitario"),
                (CanonicalField.Note, "Nota")),
            Stock = new() { Stock("EXT-1", 28m), Stock("EXT-2", 6m) },
            StockByLocation = new()
            {
                Loc("EXT-1", "Bodega", 40m), Loc("EXT-1", "Tienda", -12m),
                Loc("EXT-2", "Bodega", 6m)
            },
            StagedRows = 3,
            AppliedRows = 3,
            DefaultCurrency = "MXN"
        });
    }

    private static void EnglishHeaders(string root)
    {
        var headers = new[] { "Date", "Type", "Code", "Product", "Qty", "Unit", "Warehouse", "Supplier/Customer", "Unit Price", "Notes" };
        var rows = new[]
        {
            Row("2024-12-01", "In", "ENG-1", "Notebook", "10", "each", "Backroom", "Office Supply", "30.00", "Purchase"),
            Row("2024-12-02", "Sale", "ENG-1", "Notebook", "3", "each", "Store", "Walk-in", "45.00", "Sale"),
            Row("2024-12-03", "Adjustment", "ENG-1", "Notebook", "-1", "each", "Backroom", "Count", "0.00", "Damaged")
        };

        var dir = FixtureDir(root, "english-headers");
        WriteXlsx(Path.Combine(dir, "source.xlsx"), "Movements", headers, rows);
        WriteExpected(dir, new ExpectedOutcome
        {
            RecordType = RecordType.Mixed,
            Mapping = Mapping(
                (CanonicalField.Date, "Date"),
                (CanonicalField.Direction, "Type"),
                (CanonicalField.Sku, "Code"),
                (CanonicalField.ProductName, "Product"),
                (CanonicalField.Quantity, "Qty"),
                (CanonicalField.Unit, "Unit"),
                (CanonicalField.Location, "Warehouse"),
                (CanonicalField.PartyName, "Supplier/Customer"),
                (CanonicalField.UnitPrice, "Unit Price"),
                (CanonicalField.Note, "Notes")),
            Stock = new() { Stock("ENG-1", 6m) },
            StockByLocation = new() { Loc("ENG-1", "Backroom", 9m), Loc("ENG-1", "Store", -3m) },
            StagedRows = 3,
            AppliedRows = 3,
            DefaultCurrency = "MXN"
        });
    }

    private static void BomQuotedCsv(string root)
    {
        var rows = new[]
        {
            Row("2025-01-01", "Entrada", "SAL-ROJA", "Salsa, roja", "24", "frasco", "Bodega", "Conservas SA", "18.00", "Cliente dijo \"urgente\""),
            Row("2025-01-02", "Venta", "SAL-ROJA", "Salsa, roja", "5", "frasco", "Tienda", "Cliente mostrador", "25.00", "Venta con coma, y nota"),
            Row("2025-01-03", "Entrada", "CHI-001", "Chile \"arbol\"", "10", "bolsa", "Bodega", "Secos del Sur", "12.00", "Compra")
        };

        var dir = FixtureDir(root, "bom-quoted-csv");
        WriteCsv(Path.Combine(dir, "source.csv"), MixedHeaders, rows, emitBom: true);
        WriteExpected(dir, ExpectedMixed(
            stock: new[] { Stock("SAL-ROJA", 19m), Stock("CHI-001", 10m) },
            locations: new[]
            {
                Loc("SAL-ROJA", "Bodega", 24m), Loc("SAL-ROJA", "Tienda", -5m),
                Loc("CHI-001", "Bodega", 10m)
            },
            stagedRows: 3,
            appliedRows: 3,
            defaultCurrency: "MXN"));
    }

    private static void NoLocation(string root)
    {
        var headers = new[] { "Fecha", "Tipo Movimiento", "Código", "Producto", "Cantidad", "Unidad", "Proveedor/Cliente", "Precio Unitario", "Nota" };
        var rows = new[]
        {
            Row("2025-02-01", "Entrada", "NOL-1", "Servilleta", "30", "paquete", "Papeles SA", "8.00", "Sin ubicacion"),
            Row("2025-02-02", "Venta", "NOL-1", "Servilleta", "7", "paquete", "Cliente mostrador", "12.00", "Sin ubicacion")
        };

        var dir = FixtureDir(root, "no-location");
        WriteXlsx(Path.Combine(dir, "source.xlsx"), "Movimientos", headers, rows);
        WriteExpected(dir, new ExpectedOutcome
        {
            RecordType = RecordType.Mixed,
            Mapping = Mapping(
                (CanonicalField.Date, "Fecha"),
                (CanonicalField.Direction, "Tipo Movimiento"),
                (CanonicalField.Sku, "Código"),
                (CanonicalField.ProductName, "Producto"),
                (CanonicalField.Quantity, "Cantidad"),
                (CanonicalField.Unit, "Unidad"),
                (CanonicalField.PartyName, "Proveedor/Cliente"),
                (CanonicalField.UnitPrice, "Precio Unitario"),
                (CanonicalField.Note, "Nota")),
            Stock = new() { Stock("NOL-1", 23m) },
            StagedRows = 2,
            AppliedRows = 2,
            DefaultCurrency = "MXN"
        });
    }

    private static ExpectedOutcome ExpectedMixed(
        StockExpectation[] stock,
        LocationStockExpectation[] locations,
        int stagedRows,
        int appliedRows,
        string? defaultCurrency)
    {
        return new ExpectedOutcome
        {
            RecordType = RecordType.Mixed,
            Mapping = Mapping(
                (CanonicalField.Date, "Fecha"),
                (CanonicalField.Direction, "Tipo Movimiento"),
                (CanonicalField.Sku, "Código"),
                (CanonicalField.ProductName, "Producto"),
                (CanonicalField.Quantity, "Cantidad"),
                (CanonicalField.Unit, "Unidad"),
                (CanonicalField.Location, "Almacén"),
                (CanonicalField.PartyName, "Proveedor/Cliente"),
                (CanonicalField.UnitPrice, "Precio Unitario"),
                (CanonicalField.Note, "Nota")),
            Stock = stock.ToList(),
            StockByLocation = locations.ToList(),
            StagedRows = stagedRows,
            AppliedRows = appliedRows,
            DefaultCurrency = defaultCurrency
        };
    }

    private static ExpectedOutcome ExpectedNoDirection(
        RecordType recordType,
        string[] headers,
        string partyColumn,
        StockExpectation[] stock,
        LocationStockExpectation[] locations,
        int stagedRows,
        int appliedRows,
        string? defaultCurrency)
    {
        return new ExpectedOutcome
        {
            RecordType = recordType,
            Mapping = Mapping(
                (CanonicalField.Date, headers[0]),
                (CanonicalField.Sku, headers[1]),
                (CanonicalField.ProductName, headers[2]),
                (CanonicalField.Quantity, headers[3]),
                (CanonicalField.Unit, headers[4]),
                (CanonicalField.Location, headers[5]),
                (CanonicalField.PartyName, partyColumn),
                (CanonicalField.UnitPrice, headers[7]),
                (CanonicalField.Note, headers[8])),
            Stock = stock.ToList(),
            StockByLocation = locations.ToList(),
            StagedRows = stagedRows,
            AppliedRows = appliedRows,
            DefaultCurrency = defaultCurrency
        };
    }

    private static Dictionary<string, string?> Mapping(params (CanonicalField Field, string? Column)[] entries)
    {
        var map = Enum.GetValues<CanonicalField>()
            .ToDictionary(field => field.ToString(), _ => (string?)null, StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
            map[entry.Field.ToString()] = entry.Column;

        return map;
    }

    private static StockExpectation Stock(string sku, decimal onHand) =>
        new() { Sku = sku, ExpectedOnHand = onHand };

    private static LocationStockExpectation Loc(string product, string location, decimal onHand) =>
        new() { Product = product, Location = location, ExpectedOnHand = onHand };

    private static string[] Row(params string[] values) => values;

    private static string FixtureDir(string root, string name)
    {
        var path = Path.Combine(root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteExpected(string directory, ExpectedOutcome expected)
    {
        expected.Normalize();
        File.WriteAllText(Path.Combine(directory, "expected.json"), EvalJson.Serialize(expected));
    }

    private static void WriteXlsx(string path, string sheetName, string[] headers, IReadOnlyList<string[]> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetName);
        WriteTable(sheet, startRow: 1, headers, rows);
        workbook.SaveAs(path);
    }

    private static void WriteTitleBannerXlsx(string path, string[] headers, IReadOnlyList<string[]> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Movimientos");
        sheet.Range(1, 1, 1, headers.Length).Merge().Value = "Reporte de inventario - enero";
        sheet.Range(2, 1, 2, headers.Length).Merge().Value = "Exportado desde caja";
        WriteTable(sheet, startRow: 4, headers, rows);
        workbook.SaveAs(path);
    }

    private static void WriteTable(IXLWorksheet sheet, int startRow, string[] headers, IReadOnlyList<string[]> rows)
    {
        for (var c = 0; c < headers.Length; c++)
            sheet.Cell(startRow, c + 1).Value = headers[c];

        for (var r = 0; r < rows.Count; r++)
        {
            for (var c = 0; c < headers.Length; c++)
                sheet.Cell(startRow + r + 1, c + 1).Value = c < rows[r].Length ? rows[r][c] : "";
        }

        sheet.Columns().AdjustToContents();
    }

    private static void WriteCsv(
        string path,
        string[] headers,
        IReadOnlyList<string[]> rows,
        char delimiter = ',',
        bool emitBom = false)
    {
        var lines = new List<string> { CsvLine(headers, delimiter) };
        lines.AddRange(rows.Select(row => CsvLine(row, delimiter)));

        var encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: emitBom);
        File.WriteAllText(path, string.Join(Environment.NewLine, lines), encoding);
    }

    private static string CsvLine(IEnumerable<string> values, char delimiter) =>
        string.Join(delimiter, values.Select(value => CsvCell(value, delimiter)));

    private static string CsvCell(string value, char delimiter)
    {
        var needsQuotes = value.Contains(delimiter) || value.Contains('"') || value.Contains('\r') || value.Contains('\n');
        return needsQuotes ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }

    private static void WriteReadme(string evalsRoot)
    {
        Directory.CreateDirectory(evalsRoot);
        var lines = new[]
        {
            "# InfoOrganizer eval corpus",
            "This corpus measures the claim that messy spreadsheets can be adapted automatically.",
            "All fixture data is generated locally by the eval tool; no external data is used.",
            "Fixtures are small es-MX flavored ledgers with deliberate formatting problems.",
            "Each fixture has a source.xlsx or source.csv and a hand-authored expected.json answer key.",
            "The runner uses the real import pipeline with the unconfirmed heuristic proposal.",
            "The fake AI client is unconfigured, so evals never make network or AI calls.",
            "Run the corpus with: dotnet run --project tools/InfoOrganizer.Evals -- run",
            "Write tracking JSON with: dotnet run --project tools/InfoOrganizer.Evals -- run --json artifacts/evals/latest.json",
            "Regenerate fixtures with: dotnet run --project tools/InfoOrganizer.Evals -- generate",
            "Regeneration rewrites evals/fixtures deterministically from FixtureGenerator.cs.",
            "Known-issue fixtures stay in reports but are excluded from the hard CI gate.",
            "The xlsx files are produced with ClosedXML and csv files use deterministic UTF-8 text.",
            "The CI gate is tests/InfoOrganizer.Tests/EvalCorpusTests.cs via the normal dotnet test path.",
            "When adding fixtures, update expected.json from construction facts, not runner output."
        };
        File.WriteAllText(Path.Combine(evalsRoot, "README.md"), string.Join(Environment.NewLine, lines));
    }
}
