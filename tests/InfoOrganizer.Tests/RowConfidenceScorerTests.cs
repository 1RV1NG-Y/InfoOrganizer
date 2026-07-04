using InfoOrganizer.Application;
using InfoOrganizer.Domain;

namespace InfoOrganizer.Tests;

public class RowConfidenceScorerTests
{
    private readonly RowConfidenceScorer _scorer = new();

    [Fact]
    public void Clean_excel_row_with_confident_mapping_is_ready()
    {
        var score = _scorer.Score(CleanRow(), Mapping(), SourceType.Excel);

        Assert.Equal(ReviewRowStatus.Ready, score.SuggestedStatus);
        Assert.True(score.Confidence >= 0.9);
    }

    [Fact]
    public void Row_with_hard_issue_needs_review()
    {
        var row = CleanRow();
        row.Issues.Add("Missing quantity.");

        var score = _scorer.Score(row, Mapping(), SourceType.Excel);

        Assert.Equal(ReviewRowStatus.NeedsReview, score.SuggestedStatus);
        Assert.True(score.Confidence <= 0.5);
    }

    [Fact]
    public void Image_source_and_low_mapping_confidence_reduce_score()
    {
        var excel = _scorer.Score(CleanRow(), Mapping(), SourceType.Excel);
        var image = _scorer.Score(CleanRow(), Mapping(), SourceType.Image);
        var lowConfidenceImage = _scorer.Score(CleanRow(), Mapping(productConfidence: 0.5, quantityConfidence: 0.5), SourceType.Image);

        Assert.True(image.Confidence < excel.Confidence);
        Assert.True(lowConfidenceImage.Confidence < RowConfidenceScorer.ReadyThreshold);
        Assert.Equal(ReviewRowStatus.NeedsReview, lowConfidenceImage.SuggestedStatus);
    }

    [Fact]
    public void Parse_warning_costs_fifteen_points()
    {
        var clean = _scorer.Score(CleanRow(), Mapping(includeDate: true), SourceType.Excel);
        var warned = CleanRow();
        warned.Warnings.Add("Date could not be parsed");

        var score = _scorer.Score(warned, Mapping(includeDate: true), SourceType.Excel);

        Assert.Equal(clean.Confidence - 0.15, score.Confidence, 6);
    }

    [Fact]
    public void Confidence_is_clamped()
    {
        var high = _scorer.Score(CleanRow(), Mapping(), SourceType.Excel);
        var lowRow = CleanRow();
        lowRow.Issues.AddRange(new[] { "Missing product.", "Missing quantity.", "Unknown direction." });

        var low = _scorer.Score(lowRow, Mapping(productConfidence: 0.1, quantityConfidence: 0.1), SourceType.Image);

        Assert.Equal(0.99, high.Confidence, 6);
        Assert.Equal(0.05, low.Confidence, 6);
    }

    private static NormalizedRow CleanRow() => new()
    {
        RowIndex = 0,
        ProductName = "Cafe",
        Quantity = 10,
        Kind = MovementKind.In
    };

    private static MappingProposal Mapping(
        double productConfidence = 1.0,
        double quantityConfidence = 1.0,
        bool includeDate = false)
    {
        var fields = new List<FieldMapping>
        {
            new() { Field = CanonicalField.ProductName, SourceColumn = "Producto", Confidence = productConfidence },
            new() { Field = CanonicalField.Quantity, SourceColumn = "Cantidad", Confidence = quantityConfidence }
        };

        if (includeDate)
            fields.Add(new FieldMapping { Field = CanonicalField.Date, SourceColumn = "Fecha", Confidence = 1.0 });

        return new MappingProposal
        {
            Fields = fields,
            DetectedRecordType = RecordType.Arrivals
        };
    }
}
