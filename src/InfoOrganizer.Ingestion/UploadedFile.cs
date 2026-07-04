namespace InfoOrganizer.Ingestion;

/// <summary>An uploaded source file (spreadsheet or photo) held in memory for ingestion.</summary>
public sealed class UploadedFile
{
    public string FileName { get; init; } = "";
    public string ContentType { get; init; } = "";
    public byte[] Content { get; init; } = Array.Empty<byte>();

    public string Extension => Path.GetExtension(FileName).ToLowerInvariant();
}
