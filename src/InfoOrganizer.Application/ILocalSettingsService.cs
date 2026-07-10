namespace InfoOrganizer.Application;

public interface ILocalSettingsService
{
    string? GetAnthropicApiKey();
    bool HasSavedAnthropicApiKey();
    void SaveAnthropicApiKey(string apiKey);
    void RemoveAnthropicApiKey();
    string? GetOpenAiApiKey();
    bool HasSavedOpenAiApiKey();
    void SaveOpenAiApiKey(string apiKey);
    void RemoveOpenAiApiKey();
    string? GetSavedAiProvider();
    void SaveAiProvider(string provider);
    string? GetSavedOllamaHost();
    void SaveOllamaHost(string host);
    string? GetSavedOllamaModel();
    void SaveOllamaModel(string model);
}
