using InfoOrganizer.Domain;

namespace InfoOrganizer.Application;

/// <summary>Scores a normalized row and recommends whether it can bypass manual review.</summary>
public interface IRowConfidenceScorer
{
    /// <summary>Returns the row confidence and suggested review status.</summary>
    RowScore Score(NormalizedRow row, MappingProposal mapping, SourceType sourceType);
}

/// <summary>Machine confidence and the status implied by the review threshold.</summary>
public sealed record RowScore(double Confidence, ReviewRowStatus SuggestedStatus);

/// <summary>Simple deterministic row scorer: start from the machine ceiling, subtract 0.5 for each
/// hard issue and 0.15 for each non-fatal parse warning, multiply by mapped required-field confidence,
/// multiply image rows by 0.85 for OCR risk, then clamp to [0.05, 0.99].</summary>
public sealed class RowConfidenceScorer : IRowConfidenceScorer
{
    /// <summary>Minimum machine score for an issue-free row to be staged as ready.</summary>
    public const double ReadyThreshold = 0.72;

    private const double MaxMachineConfidence = 0.99;
    private const double MinConfidence = 0.05;

    public RowScore Score(NormalizedRow row, MappingProposal mapping, SourceType sourceType)
    {
        var score = MaxMachineConfidence;
        score -= row.Issues.Count * 0.5;
        score -= row.Warnings.Count * 0.15;
        score *= RequiredFieldFactor(mapping);

        if (sourceType == SourceType.Image)
            score *= 0.85;

        score = Math.Clamp(score, MinConfidence, MaxMachineConfidence);
        var status = row.Issues.Count == 0 && score >= ReadyThreshold
            ? ReviewRowStatus.Ready
            : ReviewRowStatus.NeedsReview;

        return new RowScore(score, status);
    }

    private static double RequiredFieldFactor(MappingProposal mapping)
    {
        var confidences = new List<double>();

        var product = MappedField(mapping, CanonicalField.ProductName)
            ?? MappedField(mapping, CanonicalField.Sku);
        if (product is not null)
            confidences.Add(product.Confidence);

        var quantity = MappedField(mapping, CanonicalField.Quantity);
        if (quantity is not null)
            confidences.Add(quantity.Confidence);

        if (confidences.Count == 0)
            return 1.0;

        return Math.Clamp(confidences.Average(), 0.85, 1.0);
    }

    private static FieldMapping? MappedField(MappingProposal mapping, CanonicalField field) =>
        mapping.Fields.FirstOrDefault(f => f.Field == field && !string.IsNullOrWhiteSpace(f.SourceColumn));
}
