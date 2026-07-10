using InfoOrganizer.Ai;
using InfoOrganizer.Domain;
using Microsoft.Extensions.Configuration;

namespace InfoOrganizer.Application;

public sealed class AiProviderRouter : IAiClient
{
    private readonly ILocalSettingsService _settings;
    private readonly IConfiguration _config;
    private readonly IAiClient _anthropic;
    private readonly IAiClient _openAi;
    private readonly IAiClient _ollama;

    public AiProviderRouter(
        ILocalSettingsService settings,
        IConfiguration config,
        IAiClient anthropic,
        IAiClient openAi,
        IAiClient ollama)
    {
        _settings = settings;
        _config = config;
        _anthropic = anthropic;
        _openAi = openAi;
        _ollama = ollama;
    }

    public bool IsConfigured => SelectedClient.IsConfigured;

    public Task<MappingProposal> ProposeMappingAsync(RawTable table, CancellationToken ct = default) =>
        SelectedClient.ProposeMappingAsync(table, ct);

    public Task<RawTable> ExtractTableFromImageAsync(byte[] imageBytes, string mediaType, string fileName, CancellationToken ct = default) =>
        SelectedClient.ExtractTableFromImageAsync(imageBytes, mediaType, fileName, ct);

    private IAiClient SelectedClient => ResolveProvider() switch
    {
        AiProviderNames.OpenAI => _openAi,
        AiProviderNames.Ollama => _ollama,
        _ => _anthropic
    };

    private string ResolveProvider() =>
        AiProviderNames.NormalizeOrDefault(_settings.GetSavedAiProvider() ?? _config["Ai:Provider"]);
}
