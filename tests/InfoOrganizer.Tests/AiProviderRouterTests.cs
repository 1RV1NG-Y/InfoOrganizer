using InfoOrganizer.Ai;
using InfoOrganizer.Application;
using InfoOrganizer.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace InfoOrganizer.Tests;

public class AiProviderRouterTests
{
    [Fact]
    public void IsConfigured_reflects_selected_provider_only()
    {
        using var temp = new TempDirectory();
        var settings = new LocalSettingsService(temp.DirectoryPath);
        var config = new TestConfiguration(new Dictionary<string, string?>());
        settings.SaveOpenAiApiKey("sk-openai-test-secret-1234");
        settings.SaveAiProvider(AiProviderNames.Anthropic);

        var router = new AiProviderRouter(
            settings,
            config,
            new AnthropicAiClient(new AnthropicOptions(), new AnthropicApiKeyProvider(settings, config)),
            new OpenAiAiClient(new OpenAiOptions(), new OpenAiApiKeyProvider(settings, config)),
            new OllamaAiClient(new OllamaOptions(), new OllamaSettingsProvider(settings, new OllamaOptions())));

        Assert.False(router.IsConfigured);

        settings.SaveAiProvider(AiProviderNames.OpenAI);

        Assert.True(router.IsConfigured);
    }

    [Fact]
    public async Task ProposeMappingAsync_delegates_to_selected_client()
    {
        using var temp = new TempDirectory();
        var settings = new LocalSettingsService(temp.DirectoryPath);
        settings.SaveAiProvider(AiProviderNames.OpenAI);

        var anthropic = new RecordingAiClient { IsConfiguredValue = true, Proposal = new MappingProposal { Rationale = "anthropic" } };
        var openAiProposal = new MappingProposal { Rationale = "openai" };
        var openAi = new RecordingAiClient { IsConfiguredValue = true, Proposal = openAiProposal };
        var router = new AiProviderRouter(settings, new TestConfiguration(new Dictionary<string, string?>()), anthropic, openAi, new RecordingAiClient());

        var result = await router.ProposeMappingAsync(new RawTable());

        Assert.Same(openAiProposal, result);
        Assert.Equal(0, anthropic.MappingCallCount);
        Assert.Equal(1, openAi.MappingCallCount);
    }

    [Fact]
    public async Task ExtractTableFromImageAsync_delegates_to_selected_client()
    {
        using var temp = new TempDirectory();
        var settings = new LocalSettingsService(temp.DirectoryPath);
        settings.SaveAiProvider(AiProviderNames.Anthropic);

        var anthropicTable = new RawTable { Meta = new SourceMeta { FileName = "anthropic.jpg" } };
        var anthropic = new RecordingAiClient { IsConfiguredValue = true, ExtractedTable = anthropicTable };
        var openAi = new RecordingAiClient { IsConfiguredValue = true, ExtractedTable = new RawTable() };
        var router = new AiProviderRouter(settings, new TestConfiguration(new Dictionary<string, string?>()), anthropic, openAi, new RecordingAiClient());

        var result = await router.ExtractTableFromImageAsync([1, 2, 3], "image/jpeg", "photo.jpg");

        Assert.Same(anthropicTable, result);
        Assert.Equal(1, anthropic.ImageCallCount);
        Assert.Equal(0, openAi.ImageCallCount);
    }

    [Fact]
    public async Task ProposeMappingAsync_delegates_to_ollama_when_selected()
    {
        using var temp = new TempDirectory();
        var settings = new LocalSettingsService(temp.DirectoryPath);
        settings.SaveAiProvider(AiProviderNames.Ollama);

        var anthropic = new RecordingAiClient { IsConfiguredValue = true, Proposal = new MappingProposal { Rationale = "anthropic" } };
        var openAi = new RecordingAiClient { IsConfiguredValue = true, Proposal = new MappingProposal { Rationale = "openai" } };
        var ollamaProposal = new MappingProposal { Rationale = "ollama" };
        var ollama = new RecordingAiClient { IsConfiguredValue = true, Proposal = ollamaProposal };
        var router = new AiProviderRouter(settings, new TestConfiguration(new Dictionary<string, string?>()), anthropic, openAi, ollama);

        var result = await router.ProposeMappingAsync(new RawTable());

        Assert.Same(ollamaProposal, result);
        Assert.Equal(0, anthropic.MappingCallCount);
        Assert.Equal(0, openAi.MappingCallCount);
        Assert.Equal(1, ollama.MappingCallCount);
    }

    private sealed class RecordingAiClient : IAiClient
    {
        public bool IsConfiguredValue { get; init; }
        public MappingProposal? Proposal { get; init; }
        public RawTable? ExtractedTable { get; init; }
        public int MappingCallCount { get; private set; }
        public int ImageCallCount { get; private set; }

        public bool IsConfigured => IsConfiguredValue;

        public Task<MappingProposal> ProposeMappingAsync(RawTable table, CancellationToken ct = default)
        {
            MappingCallCount++;
            return Task.FromResult(Proposal ?? new MappingProposal());
        }

        public Task<RawTable> ExtractTableFromImageAsync(byte[] imageBytes, string mediaType, string fileName, CancellationToken ct = default)
        {
            ImageCallCount++;
            return Task.FromResult(ExtractedTable ?? new RawTable());
        }
    }

    private sealed class TestConfiguration : IConfiguration
    {
        private readonly Dictionary<string, string?> _values;

        public TestConfiguration(Dictionary<string, string?> values) => _values = values;

        public string? this[string key]
        {
            get => _values.GetValueOrDefault(key);
            set => _values[key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren() => Array.Empty<IConfigurationSection>();
        public IChangeToken GetReloadToken() => NoopChangeToken.Instance;
        public IConfigurationSection GetSection(string key) => new TestConfigurationSection(key, this[key]);
    }

    private sealed class TestConfigurationSection : IConfigurationSection
    {
        public TestConfigurationSection(string key, string? value)
        {
            Key = key;
            Path = key;
            Value = value;
        }

        public string Key { get; }
        public string Path { get; }
        public string? Value { get; set; }

        public string? this[string key]
        {
            get => null;
            set { }
        }

        public IEnumerable<IConfigurationSection> GetChildren() => Array.Empty<IConfigurationSection>();
        public IChangeToken GetReloadToken() => NoopChangeToken.Instance;
        public IConfigurationSection GetSection(string key) => new TestConfigurationSection(key, null);
    }

    private sealed class NoopChangeToken : IChangeToken
    {
        public static readonly NoopChangeToken Instance = new();

        public bool HasChanged => false;
        public bool ActiveChangeCallbacks => false;

        public IDisposable RegisterChangeCallback(Action<object?> callback, object? state) => NoopDisposable.Instance;
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();

        public void Dispose()
        {
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), $"io-router-{Guid.NewGuid():N}");

        public void Dispose()
        {
            try { Directory.Delete(DirectoryPath, recursive: true); } catch { /* temp directory */ }
        }
    }
}
