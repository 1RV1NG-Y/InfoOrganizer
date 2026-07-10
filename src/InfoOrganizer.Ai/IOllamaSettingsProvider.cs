namespace InfoOrganizer.Ai;

public interface IOllamaSettingsProvider
{
    string? GetHost();
    string? GetModel();
}
