using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace InfoOrganizer.Application;

public sealed class LocalSettingsService : ILocalSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly object _gate = new();
    private readonly string _settingsPath;
    private SettingsFile _settings;

    public LocalSettingsService() : this(AppPaths.GetAppDataDirectory())
    {
    }

    public LocalSettingsService(string appDataDirectory)
    {
        Directory.CreateDirectory(appDataDirectory);
        _settingsPath = Path.Combine(appDataDirectory, "settings.json");
        _settings = ReadSettings();
    }

    public string? GetAnthropicApiKey()
    {
        lock (_gate)
        {
            return Unprotect(_settings.AnthropicApiKey);
        }
    }

    public bool HasSavedAnthropicApiKey()
    {
        lock (_gate)
        {
            return !string.IsNullOrWhiteSpace(_settings.AnthropicApiKey?.Value);
        }
    }

    public void SaveAnthropicApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API key is required.", nameof(apiKey));

        lock (_gate)
        {
            _settings.AnthropicApiKey = Protect(apiKey.Trim());
            WriteSettings();
        }
    }

    public void RemoveAnthropicApiKey()
    {
        lock (_gate)
        {
            _settings.AnthropicApiKey = null;
            WriteSettings();
        }
    }

    private SettingsFile ReadSettings()
    {
        if (!File.Exists(_settingsPath))
            return new SettingsFile();

        try
        {
            return JsonSerializer.Deserialize<SettingsFile>(File.ReadAllText(_settingsPath), JsonOptions)
                ?? new SettingsFile();
        }
        catch (JsonException)
        {
            return new SettingsFile();
        }
    }

    private void WriteSettings()
    {
        var json = JsonSerializer.Serialize(_settings, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }

    private static ProtectedSetting Protect(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        if (OperatingSystem.IsWindows())
        {
            bytes = ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return new ProtectedSetting("dpapi-current-user", Convert.ToBase64String(bytes));
        }

        return new ProtectedSetting("base64", Convert.ToBase64String(bytes));
    }

    /// <summary>On non-Windows platforms this falls back to plain base64 storage; the installer target is Windows.</summary>
    private static string? Unprotect(ProtectedSetting? setting)
    {
        if (string.IsNullOrWhiteSpace(setting?.Value))
            return null;

        var bytes = Convert.FromBase64String(setting.Value);
        if (setting.Protection == "dpapi-current-user" && OperatingSystem.IsWindows())
            bytes = ProtectedData.Unprotect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);

        return Encoding.UTF8.GetString(bytes);
    }

    private sealed record SettingsFile
    {
        public ProtectedSetting? AnthropicApiKey { get; set; }
    }

    private sealed record ProtectedSetting(string Protection, string Value);
}
