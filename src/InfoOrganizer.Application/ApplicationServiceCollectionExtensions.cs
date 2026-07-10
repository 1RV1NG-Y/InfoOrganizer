using InfoOrganizer.Ai;
using InfoOrganizer.Domain;
using InfoOrganizer.Data;
using InfoOrganizer.Ingestion;
using InfoOrganizer.Mapping;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InfoOrganizer.Application;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>Registers the whole pipeline: AI client, ingestion adapters, profiler, mapping engine,
    /// normalizer, profile store, and the import/tracking services. Call after <c>AddInfoOrganizerData</c>.</summary>
    public static IServiceCollection AddInfoOrganizerApplication(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<ILocalSettingsService>(_ => new LocalSettingsService(AppPaths.GetAppDataDirectory()));
        services.AddSingleton<IAnthropicApiKeyProvider, AnthropicApiKeyProvider>();
        services.AddSingleton<IOpenAiApiKeyProvider, OpenAiApiKeyProvider>();
        services.AddSingleton<IOllamaSettingsProvider, OllamaSettingsProvider>();
        services.AddSingleton(new AnthropicOptions
        {
            Model = config["Anthropic:Model"] ?? "claude-opus-4-8"
        });
        services.AddSingleton(new OpenAiOptions
        {
            Model = config["OpenAi:Model"] ?? "gpt-5.1"
        });
        services.AddSingleton(new OllamaOptions
        {
            Host = config["Ollama:Host"] ?? "http://127.0.0.1:11434",
            Model = config["Ollama:Model"] ?? "",
            Think = bool.TryParse(config["Ollama:Think"], out var think) && think,
            MaxImageEdge = ReadInt(config["Ollama:MaxImageEdge"], 1024),
            NumPredictMapping = ReadInt(config["Ollama:NumPredictMapping"], 2048),
            NumPredictImage = ReadInt(config["Ollama:NumPredictImage"], 4096)
        });
        services.AddSingleton<AnthropicAiClient>();
        services.AddSingleton<OpenAiAiClient>();
        services.AddSingleton<OllamaAiClient>();
        services.AddSingleton<IAiClient>(sp => new AiProviderRouter(
            sp.GetRequiredService<ILocalSettingsService>(),
            config,
            sp.GetRequiredService<AnthropicAiClient>(),
            sp.GetRequiredService<OpenAiAiClient>(),
            sp.GetRequiredService<OllamaAiClient>()));

        // Ingestion adapters (resolved as a set so the import service can pick by file type).
        services.AddSingleton<ISourceAdapter, ExcelSourceAdapter>();
        services.AddSingleton<ISourceAdapter, CsvSourceAdapter>();
        services.AddSingleton<ISourceAdapter, ImageSourceAdapter>();
        services.AddSingleton<IColumnProfiler, ColumnProfiler>();

        // Mapping.
        services.AddScoped<ISourceProfileStore, SourceProfileStore>();
        services.AddSingleton<HeuristicMapper>();
        services.AddScoped<IMappingEngine, MappingEngine>();

        // Application services.
        services.AddSingleton<INormalizer, Normalizer>();
        services.AddSingleton<IRowConfidenceScorer, RowConfidenceScorer>();
        services.AddScoped<ImportService>();
        services.AddScoped<TrackingService>();
        services.AddScoped<CatalogService>();
        services.AddScoped<SeedService>();

        return services;
    }

    private static int ReadInt(string? value, int fallback) =>
        int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
}
