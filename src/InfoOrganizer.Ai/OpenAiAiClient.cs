using InfoOrganizer.Domain;
using OpenAI.Chat;

namespace InfoOrganizer.Ai;

public sealed class OpenAiOptions
{
    public string Model { get; set; } = "gpt-5.1";
}

/// <summary>Real <see cref="IAiClient"/> backed by the OpenAI Chat Completions API.</summary>
public sealed class OpenAiAiClient : IAiClient
{
    private readonly OpenAiOptions _options;
    private readonly IOpenAiApiKeyProvider _keyProvider;
    private readonly object _gate = new();
    private ChatClient? _client;
    private string? _clientKey;

    public OpenAiAiClient(OpenAiOptions options, IOpenAiApiKeyProvider keyProvider)
    {
        _options = options;
        _keyProvider = keyProvider;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_keyProvider.GetApiKey());

    public async Task<MappingProposal> ProposeMappingAsync(RawTable table, CancellationToken ct = default)
    {
        var client = GetClient();

        List<ChatMessage> messages =
        [
            new SystemChatMessage(AiPromptCatalog.MappingSystemPrompt),
            new UserChatMessage(AiPromptCatalog.DescribeTable(table))
        ];

        var options = new ChatCompletionOptions
        {
            MaxOutputTokenCount = 8000,
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "mapping_proposal",
                jsonSchema: BinaryData.FromString(AiSchemas.MappingSchemaJson),
                jsonSchemaIsStrict: true)
        };

        ChatCompletion completion = await client.CompleteChatAsync(messages, options, ct);
        var json = FirstText(completion)
            ?? throw new InvalidOperationException("No text content in mapping response.");

        return AiResponseParser.ParseProposal(json, table);
    }

    public async Task<RawTable> ExtractTableFromImageAsync(byte[] imageBytes, string mediaType, string fileName, CancellationToken ct = default)
    {
        var client = GetClient();

        List<ChatMessage> messages =
        [
            new SystemChatMessage(AiPromptCatalog.ImageSystemPrompt),
            new UserChatMessage(
                ChatMessageContentPart.CreateImagePart(
                    BinaryData.FromBytes(imageBytes),
                    NormalizeImageMediaType(mediaType),
                    imageDetailLevel: null),
                ChatMessageContentPart.CreateTextPart(AiPromptCatalog.ImageUserPrompt))
        ];

        var options = new ChatCompletionOptions
        {
            MaxOutputTokenCount = 16000,
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "image_table",
                jsonSchema: BinaryData.FromString(AiSchemas.ImageSchemaJson),
                jsonSchemaIsStrict: true)
        };

        ChatCompletion completion = await client.CompleteChatAsync(messages, options, ct);
        var json = FirstText(completion)
            ?? throw new InvalidOperationException("No text content in extraction response.");

        return AiResponseParser.ParseImageTable(json, fileName);
    }

    private ChatClient GetClient()
    {
        var apiKey = _keyProvider.GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("OpenAI API key is not configured.");

        lock (_gate)
        {
            if (_client is null || _clientKey != apiKey)
            {
                _client = new ChatClient(GetModel(), apiKey);
                _clientKey = apiKey;
            }

            return _client;
        }
    }

    private string GetModel() =>
        string.IsNullOrWhiteSpace(_options.Model) ? "gpt-5.1" : _options.Model;

    private static string? FirstText(ChatCompletion completion) =>
        completion.Content.FirstOrDefault(part => !string.IsNullOrWhiteSpace(part.Text))?.Text;

    private static string NormalizeImageMediaType(string mediaType) => mediaType.ToLowerInvariant() switch
    {
        "image/png" => "image/png",
        "image/gif" => "image/gif",
        "image/webp" => "image/webp",
        _ => "image/jpeg"
    };
}
