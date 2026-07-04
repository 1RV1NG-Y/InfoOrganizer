using InfoOrganizer.Domain;

namespace InfoOrganizer.Ingestion;

/// <summary>Reduces an uploaded file of a particular kind (Excel, image, …) to one or more
/// neutral <see cref="RawTable"/>s. A workbook may yield several tables (one per sheet);
/// a photo yields one.</summary>
public interface ISourceAdapter
{
    bool CanHandle(UploadedFile file);
    Task<IReadOnlyList<RawTable>> ExtractAsync(UploadedFile file, CancellationToken ct = default);
}
