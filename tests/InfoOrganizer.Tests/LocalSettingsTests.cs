using InfoOrganizer.Ai;
using InfoOrganizer.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace InfoOrganizer.Tests;

public class LocalSettingsTests
{
    [Fact]
    public void Anthropic_key_round_trips_without_plaintext_on_windows()
    {
        using var temp = new TempDirectory();
        const string key = "sk-ant-test-secret-1234";

        new LocalSettingsService(temp.DirectoryPath).SaveAnthropicApiKey(key);

        var loaded = new LocalSettingsService(temp.DirectoryPath).GetAnthropicApiKey();
        Assert.Equal(key, loaded);

        var settingsJson = File.ReadAllText(Path.Combine(temp.DirectoryPath, "settings.json"));
        if (OperatingSystem.IsWindows())
            Assert.False(settingsJson.Contains(key, StringComparison.Ordinal));
    }

    [Fact]
    public void OpenAI_key_round_trips_without_plaintext_on_windows()
    {
        using var temp = new TempDirectory();
        const string key = "sk-openai-test-secret-1234";

        new LocalSettingsService(temp.DirectoryPath).SaveOpenAiApiKey(key);

        var loaded = new LocalSettingsService(temp.DirectoryPath).GetOpenAiApiKey();
        Assert.Equal(key, loaded);

        var settingsJson = File.ReadAllText(Path.Combine(temp.DirectoryPath, "settings.json"));
        if (OperatingSystem.IsWindows())
            Assert.False(settingsJson.Contains(key, StringComparison.Ordinal));
    }

    [Fact]
    public void Anthropic_and_openai_keys_are_stored_under_separate_entries()
    {
        using var temp = new TempDirectory();
        const string anthropicKey = "sk-ant-test-secret-1234";
        const string openAiKey = "sk-openai-test-secret-1234";

        var settings = new LocalSettingsService(temp.DirectoryPath);
        settings.SaveAnthropicApiKey(anthropicKey);
        settings.SaveOpenAiApiKey(openAiKey);

        var loaded = new LocalSettingsService(temp.DirectoryPath);
        Assert.Equal(anthropicKey, loaded.GetAnthropicApiKey());
        Assert.Equal(openAiKey, loaded.GetOpenAiApiKey());

        var settingsJson = File.ReadAllText(Path.Combine(temp.DirectoryPath, "settings.json"));
        Assert.Contains("\"AnthropicApiKey\"", settingsJson, StringComparison.Ordinal);
        Assert.Contains("\"OpenAiApiKey\"", settingsJson, StringComparison.Ordinal);
        if (OperatingSystem.IsWindows())
        {
            Assert.False(settingsJson.Contains(anthropicKey, StringComparison.Ordinal));
            Assert.False(settingsJson.Contains(openAiKey, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Ai_provider_setting_round_trips_as_plain_value()
    {
        using var temp = new TempDirectory();

        new LocalSettingsService(temp.DirectoryPath).SaveAiProvider(AiProviderNames.OpenAI);

        var loaded = new LocalSettingsService(temp.DirectoryPath).GetSavedAiProvider();
        Assert.Equal(AiProviderNames.OpenAI, loaded);

        var settingsJson = File.ReadAllText(Path.Combine(temp.DirectoryPath, "settings.json"));
        Assert.Contains("\"AiProvider\"", settingsJson, StringComparison.Ordinal);
        Assert.Contains("\"OpenAI\"", settingsJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Remove_key_makes_load_return_null()
    {
        using var temp = new TempDirectory();
        var settings = new LocalSettingsService(temp.DirectoryPath);

        settings.SaveAnthropicApiKey("sk-ant-test-secret-1234");
        settings.RemoveAnthropicApiKey();

        Assert.Null(new LocalSettingsService(temp.DirectoryPath).GetAnthropicApiKey());
    }

    [Fact]
    public void Api_key_provider_prefers_saved_key_then_config()
    {
        using var temp = new TempDirectory();
        var settings = new LocalSettingsService(temp.DirectoryPath);
        var config = new TestConfiguration(new Dictionary<string, string?>
        {
            ["Anthropic:ApiKey"] = "config-key"
        });
        var provider = new AnthropicApiKeyProvider(settings, config);

        Assert.Equal("config-key", provider.GetApiKey());

        settings.SaveAnthropicApiKey("saved-key");

        Assert.Equal("saved-key", provider.GetApiKey());
    }

    [Fact]
    public void OpenAI_api_key_provider_prefers_saved_key_then_config()
    {
        using var temp = new TempDirectory();
        var settings = new LocalSettingsService(temp.DirectoryPath);
        var config = new TestConfiguration(new Dictionary<string, string?>
        {
            ["OpenAi:ApiKey"] = "config-key"
        });
        var provider = new OpenAiApiKeyProvider(settings, config);

        Assert.Equal("config-key", provider.GetApiKey());

        settings.SaveOpenAiApiKey("saved-key");

        Assert.Equal("saved-key", provider.GetApiKey());
    }

    [Fact]
    public void Anthropic_client_configuration_reflects_runtime_provider_change()
    {
        var provider = new MutableKeyProvider();
        var client = new AnthropicAiClient(new AnthropicOptions(), provider);

        Assert.False(client.IsConfigured);

        provider.ApiKey = "sk-ant-test-secret-1234";

        Assert.True(client.IsConfigured);
    }

    private sealed class MutableKeyProvider : IAnthropicApiKeyProvider
    {
        public string? ApiKey { get; set; }
        public string? GetApiKey() => ApiKey;
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
        public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), $"io-settings-{Guid.NewGuid():N}");

        public void Dispose()
        {
            try { Directory.Delete(DirectoryPath, recursive: true); } catch { /* temp directory */ }
        }
    }
}
