namespace InfoOrganizer.Application;

public static class AiProviderNames
{
    public const string Anthropic = "Anthropic";
    public const string OpenAI = "OpenAI";
    public const string Ollama = "Ollama";

    public static string NormalizeOrDefault(string? provider)
    {
        if (provider?.Equals(OpenAI, StringComparison.OrdinalIgnoreCase) == true)
            return OpenAI;

        if (provider?.Equals(Ollama, StringComparison.OrdinalIgnoreCase) == true ||
            provider?.Equals("local", StringComparison.OrdinalIgnoreCase) == true)
            return Ollama;

        return Anthropic;
    }

    public static bool IsKnown(string? provider) =>
        provider?.Equals(Anthropic, StringComparison.OrdinalIgnoreCase) == true ||
        provider?.Equals(OpenAI, StringComparison.OrdinalIgnoreCase) == true ||
        provider?.Equals(Ollama, StringComparison.OrdinalIgnoreCase) == true ||
        provider?.Equals("local", StringComparison.OrdinalIgnoreCase) == true;
}
