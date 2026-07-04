namespace InfoOrganizer.Application;

public interface ILocalSettingsService
{
    string? GetAnthropicApiKey();
    bool HasSavedAnthropicApiKey();
    void SaveAnthropicApiKey(string apiKey);
    void RemoveAnthropicApiKey();
}
