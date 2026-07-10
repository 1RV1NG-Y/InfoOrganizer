using InfoOrganizer.Ai;

namespace InfoOrganizer.Application;

public sealed class OllamaSettingsProvider : IOllamaSettingsProvider
{
    private readonly ILocalSettingsService _settings;
    private readonly OllamaOptions _options;

    public OllamaSettingsProvider(ILocalSettingsService settings, OllamaOptions options)
    {
        _settings = settings;
        _options = options;
    }

    public string? GetHost()
    {
        var saved = _settings.GetSavedOllamaHost();
        if (!string.IsNullOrWhiteSpace(saved))
            return saved;

        return string.IsNullOrWhiteSpace(_options.Host) ? null : _options.Host;
    }

    public string? GetModel()
    {
        var saved = _settings.GetSavedOllamaModel();
        if (!string.IsNullOrWhiteSpace(saved))
            return saved;

        return string.IsNullOrWhiteSpace(_options.Model) ? null : _options.Model;
    }
}
