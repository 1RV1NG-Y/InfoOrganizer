using Anthropic;
using Anthropic.Models.Messages;
using InfoOrganizer.Domain;

namespace InfoOrganizer.Ai;

public sealed class AnthropicOptions
{
    public string Model { get; set; } = "claude-opus-4-8";
}

/// <summary>Real <see cref="IAiClient"/> backed by the Anthropic C# SDK. Uses structured outputs so the
/// model returns strict JSON for both column mapping and (in P5) photo table extraction.</summary>
public sealed class AnthropicAiClient : IAiClient
{
    private readonly IAnthropicApiKeyProvider _keyProvider;
    private readonly object _gate = new();
    private AnthropicClient? _client;
    private string? _clientKey;

    public AnthropicAiClient(AnthropicOptions options, IAnthropicApiKeyProvider keyProvider)
    {
        _keyProvider = keyProvider;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_keyProvider.GetApiKey());

    public async Task<MappingProposal> ProposeMappingAsync(RawTable table, CancellationToken ct = default)
    {
        var client = GetClient();

        var parameters = new MessageCreateParams
        {
            Model = Model.ClaudeOpus4_8,
            MaxTokens = 8000,
            Thinking = new ThinkingConfigAdaptive(),
            OutputConfig = new OutputConfig
            {
                Effort = Effort.Medium,
                Format = new JsonOutputFormat { Schema = AiSchemas.MappingSchema() }
            },
            System = new List<TextBlockParam>
            {
                new() { Text = AiPromptCatalog.MappingSystemPrompt, CacheControl = new CacheControlEphemeral() }
            },
            Messages = [new() { Role = Role.User, Content = AiPromptCatalog.DescribeTable(table) }]
        };

        var response = await client.Messages.Create(parameters);
        var json = response.Content.Select(b => b.Value).OfType<TextBlock>().FirstOrDefault()?.Text
            ?? throw new InvalidOperationException("No text content in mapping response.");

        return AiResponseParser.ParseProposal(json, table);
    }

    public async Task<RawTable> ExtractTableFromImageAsync(byte[] imageBytes, string mediaType, string fileName, CancellationToken ct = default)
    {
        var client = GetClient();

        var parameters = new MessageCreateParams
        {
            Model = Model.ClaudeOpus4_8,
            MaxTokens = 16000,
            Thinking = new ThinkingConfigAdaptive(),
            OutputConfig = new OutputConfig
            {
                Effort = Effort.High, // messy/handwritten photos benefit from more effort
                Format = new JsonOutputFormat { Schema = AiSchemas.ImageSchema() }
            },
            System = new List<TextBlockParam>
            {
                new() { Text = AiPromptCatalog.ImageSystemPrompt, CacheControl = new CacheControlEphemeral() }
            },
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = new List<ContentBlockParam>
                    {
                        new ImageBlockParam
                        {
                            Source = new Base64ImageSource
                            {
                                Data = Convert.ToBase64String(imageBytes),
                                MediaType = ToMediaType(mediaType)
                            }
                        },
                        new TextBlockParam { Text = AiPromptCatalog.ImageUserPrompt }
                    }
                }
            ]
        };

        var response = await client.Messages.Create(parameters);
        var json = response.Content.Select(b => b.Value).OfType<TextBlock>().FirstOrDefault()?.Text
            ?? throw new InvalidOperationException("No text content in extraction response.");

        return AiResponseParser.ParseImageTable(json, fileName);
    }

    private AnthropicClient GetClient()
    {
        var apiKey = _keyProvider.GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Anthropic API key is not configured.");

        lock (_gate)
        {
            if (_client is null || _clientKey != apiKey)
            {
                _client = new AnthropicClient { ApiKey = apiKey };
                _clientKey = apiKey;
            }

            return _client;
        }
    }

    private static MediaType ToMediaType(string mediaType) => mediaType.ToLowerInvariant() switch
    {
        "image/png" => MediaType.ImagePng,
        "image/gif" => MediaType.ImageGif,
        "image/webp" => MediaType.ImageWebP,
        _ => MediaType.ImageJpeg
    };
}
