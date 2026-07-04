using InfoOrganizer.Domain;
using InfoOrganizer.Mapping;
using InfoOrganizer.Tests.Support;

namespace InfoOrganizer.Tests;

public class SchemaFingerprintTests
{
    [Fact]
    public void Is_order_case_accent_and_whitespace_insensitive()
    {
        var a = SchemaFingerprint.Compute(new[] { "Producto", "Cantidad", "Precio" });
        var b = SchemaFingerprint.Compute(new[] { " precio ", "PRODUCTO", "cantidad" });
        Assert.Equal(a, b);
    }

    [Fact]
    public void Differs_when_columns_differ()
    {
        var a = SchemaFingerprint.Compute(new[] { "Producto", "Cantidad" });
        var b = SchemaFingerprint.Compute(new[] { "Producto", "Cantidad", "Precio" });
        Assert.NotEqual(a, b);
    }
}

public class HeuristicMapperTests
{
    private static string? Col(MappingProposal p, CanonicalField f) =>
        p.Fields.First(x => x.Field == f).SourceColumn;

    [Fact]
    public void Maps_english_headers()
    {
        var table = Tables.Make("stock.xlsx",
            ("Product", RawCellType.Text), ("Quantity", RawCellType.Number),
            ("Unit Price", RawCellType.Currency), ("Date", RawCellType.Date));

        var p = new HeuristicMapper().Propose(table);

        Assert.Equal("Product", Col(p, CanonicalField.ProductName));
        Assert.Equal("Quantity", Col(p, CanonicalField.Quantity));
        Assert.Equal("Unit Price", Col(p, CanonicalField.UnitPrice));
        Assert.Equal("Date", Col(p, CanonicalField.Date));
    }

    [Fact]
    public void Maps_spanish_headers_and_detects_sales()
    {
        var table = Tables.Make("ventas-marzo.xlsx",
            ("Producto", RawCellType.Text), ("Cantidad", RawCellType.Number),
            ("Precio", RawCellType.Currency), ("Fecha", RawCellType.Date));

        var p = new HeuristicMapper().Propose(table);

        Assert.Equal("Producto", Col(p, CanonicalField.ProductName));
        Assert.Equal("Cantidad", Col(p, CanonicalField.Quantity));
        Assert.Equal("Precio", Col(p, CanonicalField.UnitPrice));
        Assert.Equal("Fecha", Col(p, CanonicalField.Date));
        Assert.Equal(RecordType.Sales, p.DetectedRecordType);
    }

    [Fact]
    public void Maps_mixed_inventory_location_and_uses_local_currency_default()
    {
        var table = Tables.Make("movimientos.xlsx",
            ("Fecha", RawCellType.Date), ("Tipo Movimiento", RawCellType.Text),
            ("Codigo", RawCellType.Text), ("Producto", RawCellType.Text),
            ("Cantidad", RawCellType.Number), ("Almacen", RawCellType.Text),
            ("Precio Unitario", RawCellType.Currency));

        var p = new HeuristicMapper().Propose(table);

        Assert.Equal("Tipo Movimiento", Col(p, CanonicalField.Direction));
        Assert.Equal("Almacen", Col(p, CanonicalField.Location));
        Assert.Equal(RecordType.Mixed, p.DetectedRecordType);
        Assert.Equal("MXN", p.Hints.DefaultCurrency);
    }

    [Fact]
    public void Maps_abbreviated_spanish_headers()
    {
        var table = Tables.Make("movimientos.xlsx",
            ("Fec.", RawCellType.Date), ("Tipo Mov.", RawCellType.Text),
            ("Cod.", RawCellType.Text), ("Producto", RawCellType.Text),
            ("Cant.", RawCellType.Number), ("Unid.", RawCellType.Text),
            ("Suc.", RawCellType.Text), ("Prov.", RawCellType.Text),
            ("P.U.", RawCellType.Currency), ("Nota", RawCellType.Text));

        var p = new HeuristicMapper().Propose(table);

        Assert.Equal("Fec.", Col(p, CanonicalField.Date));
        Assert.Equal("Tipo Mov.", Col(p, CanonicalField.Direction));
        Assert.Equal("Cod.", Col(p, CanonicalField.Sku));
        Assert.Equal("Cant.", Col(p, CanonicalField.Quantity));
        Assert.Equal("Unid.", Col(p, CanonicalField.Unit));
        Assert.Equal("Suc.", Col(p, CanonicalField.Location));
        Assert.Equal("Prov.", Col(p, CanonicalField.PartyName));
        Assert.Equal("P.U.", Col(p, CanonicalField.UnitPrice));
        Assert.Equal(RecordType.Mixed, p.DetectedRecordType);
    }

    [Fact]
    public void Direction_word_samples_beat_ambiguous_type_header()
    {
        var table = new RawTable
        {
            Meta = new SourceMeta { FileName = "movements.xlsx", SourceType = SourceType.Excel },
            Columns = new()
            {
                new RawColumn { Name = "Product", InferredType = RawCellType.Text },
                new RawColumn
                {
                    Name = "Type",
                    InferredType = RawCellType.Text,
                    SampleValues = new() { "In", "Sale", "Adjustment" }
                },
                new RawColumn { Name = "Qty", InferredType = RawCellType.Number }
            }
        };

        var p = new HeuristicMapper().Propose(table);

        Assert.Equal("Type", Col(p, CanonicalField.Direction));
        Assert.Null(Col(p, CanonicalField.Category));
        Assert.Equal(RecordType.Mixed, p.DetectedRecordType);
    }

    [Fact]
    public void Duplicate_ambiguous_headers_prefer_type_compatible_column()
    {
        var table = new RawTable
        {
            Meta = new SourceMeta { FileName = "movimientos.xlsx", SourceType = SourceType.Excel },
            Columns = new()
            {
                new RawColumn { Name = "Producto", InferredType = RawCellType.Text },
                new RawColumn
                {
                    Name = "Cantidad",
                    InferredType = RawCellType.Text,
                    SampleValues = new() { "caja", "pieza", "caja" }
                },
                new RawColumn
                {
                    Name = "Cantidad (2)",
                    InferredType = RawCellType.Number,
                    SampleValues = new() { "10", "3", "4" }
                }
            }
        };

        var p = new HeuristicMapper().Propose(table);

        Assert.Equal("Cantidad (2)", Col(p, CanonicalField.Quantity));
    }
}

public class MappingEngineTests
{
    private static RawTable SpanishTable() => Tables.Make("inv.xlsx",
        ("Producto", RawCellType.Text), ("Cantidad", RawCellType.Number));

    [Fact]
    public async Task Falls_back_to_heuristic_and_requires_confirmation_when_ai_unconfigured()
    {
        var engine = new MappingEngine(new FakeProfileStore(), new HeuristicMapper(), new FakeAiClient { IsConfigured = false });

        var result = await engine.ResolveAsync(SpanishTable());

        Assert.True(result.RequiresConfirmation);
        Assert.Equal(MappingSource.Heuristic, result.Source);
        Assert.Equal("Producto", result.Proposal.Column(CanonicalField.ProductName));
    }

    [Fact]
    public async Task Reuses_saved_profile_without_confirmation()
    {
        var table = SpanishTable();
        var fingerprint = SchemaFingerprint.Compute(table.ColumnNames);
        var profile = new SourceProfile
        {
            Id = 7,
            Fingerprint = fingerprint,
            Name = "Spanish inventory",
            DefaultRecordType = RecordType.Arrivals,
            MappingJson = MappingSerializer.SerializeFields(new[]
            {
                new FieldMapping { Field = CanonicalField.ProductName, SourceColumn = "Producto", Confidence = 1 },
                new FieldMapping { Field = CanonicalField.Quantity, SourceColumn = "Cantidad", Confidence = 1 }
            })
        };

        var engine = new MappingEngine(new FakeProfileStore(profile), new HeuristicMapper(), new FakeAiClient { IsConfigured = false });
        var result = await engine.ResolveAsync(table);

        Assert.False(result.RequiresConfirmation);
        Assert.Equal(MappingSource.SavedProfile, result.Source);
        Assert.Equal(7, result.SourceProfileId);
        Assert.Equal(RecordType.Arrivals, result.Proposal.DetectedRecordType);
        Assert.Equal("Cantidad", result.Proposal.Column(CanonicalField.Quantity));
    }

    [Fact]
    public async Task Saved_profile_backfills_new_canonical_fields_without_changing_saved_choices()
    {
        var table = Tables.Make("movimientos.xlsx",
            ("Producto", RawCellType.Text), ("Cantidad", RawCellType.Number), ("Almacen", RawCellType.Text));
        var profile = new SourceProfile
        {
            Id = 9,
            Fingerprint = SchemaFingerprint.Compute(table.ColumnNames),
            Name = "Older profile",
            DefaultRecordType = RecordType.Arrivals,
            MappingJson = MappingSerializer.SerializeFields(new[]
            {
                new FieldMapping { Field = CanonicalField.ProductName, SourceColumn = "Producto", Confidence = 1 },
                new FieldMapping { Field = CanonicalField.Quantity, SourceColumn = "Cantidad", Confidence = 1 }
            })
        };

        var engine = new MappingEngine(new FakeProfileStore(profile), new HeuristicMapper(), new FakeAiClient { IsConfigured = false });
        var result = await engine.ResolveAsync(table);

        Assert.False(result.RequiresConfirmation);
        Assert.Equal(MappingSource.SavedProfile, result.Source);
        Assert.Equal("Producto", result.Proposal.Column(CanonicalField.ProductName));
        Assert.Equal("Almacen", result.Proposal.Column(CanonicalField.Location));
    }
}
