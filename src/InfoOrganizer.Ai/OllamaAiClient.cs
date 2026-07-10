using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using InfoOrganizer.Domain;

namespace InfoOrganizer.Ai;

public sealed class OllamaOptions
{
    public string Host { get; set; } = "http://127.0.0.1:11434";
    public string Model { get; set; } = "";
    public bool Think { get; set; }
    public int MaxImageEdge { get; set; } = 1024;
    public int NumPredictMapping { get; set; } = 2048;
    public int NumPredictImage { get; set; } = 4096;
}

public sealed class OllamaAiClient : IAiClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly OllamaOptions _options;
    private readonly IOllamaSettingsProvider _settings;

    public OllamaAiClient(OllamaOptions options, IOllamaSettingsProvider settings)
    {
        _options = options;
        _settings = settings;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(GetHost()) && !string.IsNullOrWhiteSpace(GetModel());

    public async Task<MappingProposal> ProposeMappingAsync(RawTable table, CancellationToken ct = default)
    {
        var request = OllamaRequestFactory.BuildMappingRequest(
            GetRequiredModel(),
            AiPromptCatalog.DescribeTable(table),
            _options.Think,
            _options.NumPredictMapping);

        var content = await PostChatAsync(request, ct);
        return AiResponseParser.ParseProposal(content, table);
    }

    public async Task<RawTable> ExtractTableFromImageAsync(byte[] imageBytes, string mediaType, string fileName, CancellationToken ct = default)
    {
        var resized = ImageDownscaler.DownscaleToPng(imageBytes, _options.MaxImageEdge);
        var imageBase64 = Convert.ToBase64String(resized);
        var request = OllamaRequestFactory.BuildImageRequest(
            GetRequiredModel(),
            imageBase64,
            _options.Think,
            _options.NumPredictImage);

        var content = await PostChatAsync(request, ct);
        return AiResponseParser.ParseImageTable(content, fileName);
    }

    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct = default)
    {
        var host = GetRequiredHost();
        try
        {
            var tags = await Http.GetFromJsonAsync<OllamaTagsResponse>(new Uri(new Uri(host), "/api/tags"), JsonOptions, ct)
                ?? new OllamaTagsResponse([]);

            return tags.Models
                .Select(m => m.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or UriFormatException)
        {
            throw new InvalidOperationException($"Could not connect to the Ollama server at {host}. Check that Ollama is running and the host is correct.", ex);
        }
    }

    private async Task<string> PostChatAsync(OllamaChatRequest request, CancellationToken ct)
    {
        var host = GetRequiredHost();
        try
        {
            using var response = await Http.PostAsJsonAsync(new Uri(new Uri(host), "/api/chat"), request, JsonOptions, ct);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(JsonOptions, ct)
                ?? throw new InvalidOperationException("Ollama returned an empty response.");

            var content = body.Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
                content = body.Response;

            return !string.IsNullOrWhiteSpace(content)
                ? content
                : throw new InvalidOperationException("No text content in Ollama response.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or UriFormatException)
        {
            throw new InvalidOperationException($"Could not connect to the Ollama server at {host}. Check that Ollama is running and the selected model is available.", ex);
        }
    }

    private string GetRequiredHost() =>
        !string.IsNullOrWhiteSpace(GetHost())
            ? GetHost()!.Trim()
            : throw new InvalidOperationException("Ollama host is not configured.");

    private string GetRequiredModel() =>
        !string.IsNullOrWhiteSpace(GetModel())
            ? GetModel()!.Trim()
            : throw new InvalidOperationException("Ollama model is not configured.");

    private string? GetHost() => _settings.GetHost();
    private string? GetModel() => _settings.GetModel();
}

internal static class OllamaRequestFactory
{
    public static OllamaChatRequest BuildMappingRequest(string model, string tableDescription, bool think, int numPredict) =>
        new(
            model,
            [
                new OllamaMessage("system", AiPromptCatalog.MappingSystemPrompt, null),
                new OllamaMessage("user", tableDescription, null)
            ],
            false,
            think,
            ParseSchema(AiSchemas.MappingSchemaJson),
            new OllamaRequestOptions(0, numPredict));

    public static OllamaChatRequest BuildImageRequest(string model, string imageBase64, bool think, int numPredict) =>
        new(
            model,
            [
                new OllamaMessage("system", AiPromptCatalog.ImageSystemPrompt, null),
                new OllamaMessage("user", AiPromptCatalog.ImageUserPrompt, [imageBase64])
            ],
            false,
            think,
            ParseSchema(AiSchemas.ImageSchemaJson),
            new OllamaRequestOptions(0, numPredict));

    private static JsonElement ParseSchema(string schemaJson)
    {
        using var doc = JsonDocument.Parse(schemaJson);
        return doc.RootElement.Clone();
    }
}

internal sealed record OllamaChatRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] IReadOnlyList<OllamaMessage> Messages,
    [property: JsonPropertyName("stream")] bool Stream,
    [property: JsonPropertyName("think")] bool Think,
    [property: JsonPropertyName("format")] JsonElement Format,
    [property: JsonPropertyName("options")] OllamaRequestOptions Options);

internal sealed record OllamaMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("images")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? Images);

internal sealed record OllamaRequestOptions(
    [property: JsonPropertyName("temperature")] double Temperature,
    [property: JsonPropertyName("num_predict")] int NumPredict);

internal sealed record OllamaChatResponse(
    [property: JsonPropertyName("message")] OllamaResponseMessage? Message,
    [property: JsonPropertyName("response")] string? Response);

internal sealed record OllamaResponseMessage([property: JsonPropertyName("content")] string? Content);

internal sealed record OllamaTagsResponse([property: JsonPropertyName("models")] IReadOnlyList<OllamaModelTag> Models);

internal sealed record OllamaModelTag([property: JsonPropertyName("name")] string Name);
