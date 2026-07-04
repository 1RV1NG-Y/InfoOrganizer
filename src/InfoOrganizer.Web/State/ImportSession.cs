using InfoOrganizer.Application;

namespace InfoOrganizer.Web.State;

/// <summary>Per-circuit holder that carries the prepared import from the Upload screen to the Review
/// screen without round-tripping large objects through the URL.</summary>
public sealed class ImportSession
{
    public IReadOnlyList<ImportPreview> Previews { get; set; } = Array.Empty<ImportPreview>();

    public bool HasWork => Previews.Count > 0;

    public void Clear() => Previews = Array.Empty<ImportPreview>();
}
