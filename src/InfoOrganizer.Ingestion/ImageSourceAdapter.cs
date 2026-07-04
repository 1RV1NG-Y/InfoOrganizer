using InfoOrganizer.Ai;
using InfoOrganizer.Domain;

namespace InfoOrganizer.Ingestion;

/// <summary>Turns a photo of a paper record into the same neutral <see cref="RawTable"/> as Excel by
/// asking the vision model to transcribe it — so the rest of the pipeline is identical for photos.</summary>
public sealed class ImageSourceAdapter : ISourceAdapter
{
    private static readonly string[] Extensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    private readonly IAiClient _ai;

    public ImageSourceAdapter(IAiClient ai) => _ai = ai;

    public bool CanHandle(UploadedFile file) =>
        Extensions.Contains(file.Extension)
        || file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<RawTable>> ExtractAsync(UploadedFile file, CancellationToken ct = default)
    {
        if (!_ai.IsConfigured)
            throw new InvalidOperationException("Reading photos needs an Anthropic API key — set ANTHROPIC_API_KEY.");

        var table = await _ai.ExtractTableFromImageAsync(file.Content, MediaTypeFor(file), file.FileName, ct);
        return table.Rows.Count > 0 ? new[] { table } : Array.Empty<RawTable>();
    }

    private static string MediaTypeFor(UploadedFile file) =>
        file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            ? file.ContentType.ToLowerInvariant()
            : file.Extension switch
            {
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };
}
