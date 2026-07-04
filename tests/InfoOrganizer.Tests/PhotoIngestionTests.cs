using InfoOrganizer.Application;
using InfoOrganizer.Data;
using InfoOrganizer.Domain;
using InfoOrganizer.Ingestion;
using InfoOrganizer.Mapping;
using InfoOrganizer.Tests.Support;

namespace InfoOrganizer.Tests;

public class PhotoIngestionTests
{
    private static RawTable PhotoTable() => new()
    {
        Meta = new SourceMeta { SourceType = SourceType.Image, FileName = "foto.jpg" },
        Columns = new() { new RawColumn { Name = "Producto" }, new RawColumn { Name = "Cantidad" } },
        Rows = new()
        {
            new RawRow { Index = 0, Cells = new() { ["Producto"] = "Manzana", ["Cantidad"] = "100" } },
            new RawRow { Index = 1, Cells = new() { ["Producto"] = "Pera", ["Cantidad"] = "40" } },
        }
    };

    [Theory]
    [InlineData("foto.jpg", "", true)]
    [InlineData("scan.PNG", "", true)]
    [InlineData("note", "image/jpeg", true)]
    [InlineData("data.xlsx", "", false)]
    public void Image_adapter_handles_image_files(string name, string contentType, bool expected)
    {
        var adapter = new ImageSourceAdapter(new FakeAiClient { IsConfigured = true });
        Assert.Equal(expected, adapter.CanHandle(new UploadedFile { FileName = name, ContentType = contentType }));
    }

    [Fact]
    public async Task Photo_flows_through_the_same_pipeline_as_excel()
    {
        using var db = new TestDb();
        var ai = new FakeAiClient { IsConfigured = true, ExtractedTable = PhotoTable() };
        var store = new SourceProfileStore(db);
        var engine = new MappingEngine(store, new HeuristicMapper(), ai);
        var service = new ImportService(
            new ISourceAdapter[] { new ImageSourceAdapter(ai) },
            new ColumnProfiler(),
            engine,
            new Normalizer(),
            new RowConfidenceScorer(),
            store,
            db);

        var preview = (await service.PrepareAsync(
            new UploadedFile { FileName = "foto.jpg", ContentType = "image/jpeg", Content = new byte[] { 1, 2, 3 } })).Single();

        Assert.Equal(SourceType.Image, preview.SourceType);
        Assert.Equal(new[] { "Producto", "Cantidad" }, preview.Table.ColumnNames);
        Assert.Equal("Producto", preview.Resolution.Proposal.Column(CanonicalField.ProductName));
        Assert.Equal("Cantidad", preview.Resolution.Proposal.Column(CanonicalField.Quantity));

        var result = await service.CommitAsync(preview, preview.Resolution.Proposal, "Foto", saveProfile: false);
        Assert.Equal(2, result.ImportedRows);

        var levels = await new TrackingService(db).GetStockLevelsAsync();
        Assert.Equal(100m, levels.Single(l => l.Name == "Manzana").OnHand);
    }
}
