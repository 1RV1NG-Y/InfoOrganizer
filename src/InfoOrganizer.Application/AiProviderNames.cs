namespace InfoOrganizer.Application;

public static class AiProviderNames
{
    public const string Anthropic = "Anthropic";
    public const string OpenAI = "OpenAI";

    public static string NormalizeOrDefault(string? provider)
    {
        if (provider?.Equals(OpenAI, StringComparison.OrdinalIgnoreCase) == true)
            return OpenAI;

        return Anthropic;
    }

    public static bool IsKnown(string? provider) =>
        provider?.Equals(Anthropic, StringComparison.OrdinalIgnoreCase) == true ||
        provider?.Equals(OpenAI, StringComparison.OrdinalIgnoreCase) == true;
}
