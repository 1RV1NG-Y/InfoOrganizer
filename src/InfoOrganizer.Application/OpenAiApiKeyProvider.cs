using InfoOrganizer.Ai;
using Microsoft.Extensions.Configuration;

namespace InfoOrganizer.Application;

public sealed class OpenAiApiKeyProvider : IOpenAiApiKeyProvider
{
    private readonly ILocalSettingsService _settings;
    private readonly IConfiguration _config;

    public OpenAiApiKeyProvider(ILocalSettingsService settings, IConfiguration config)
    {
        _settings = settings;
        _config = config;
    }

    public string? GetApiKey()
    {
        var saved = _settings.GetOpenAiApiKey();
        if (!string.IsNullOrWhiteSpace(saved))
            return saved;

        var configured = _config["OpenAi:ApiKey"];
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        return Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    }
}
