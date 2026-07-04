using InfoOrganizer.Ai;
using Microsoft.Extensions.Configuration;

namespace InfoOrganizer.Application;

public sealed class AnthropicApiKeyProvider : IAnthropicApiKeyProvider
{
    private readonly ILocalSettingsService _settings;
    private readonly IConfiguration _config;

    public AnthropicApiKeyProvider(ILocalSettingsService settings, IConfiguration config)
    {
        _settings = settings;
        _config = config;
    }

    public string? GetApiKey()
    {
        var saved = _settings.GetAnthropicApiKey();
        if (!string.IsNullOrWhiteSpace(saved))
            return saved;

        var configured = _config["Anthropic:ApiKey"];
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        return Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
    }
}
