namespace InteriorDesignWeb.Services.AI;

public static class NegativePromptPolicy
{
    public const string SystemPrompt =
        "low quality,blurry,noise,grainy,overexposed,underexposed,bad lighting,bad composition,distorted,deformed,ugly,messy,unrealistic,cartoon,anime";

    public static string Compose(string? userPrompt)
    {
        var normalized = userPrompt?.Trim() ?? string.Empty;
        if (normalized.StartsWith(SystemPrompt, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[SystemPrompt.Length..]
                .TrimStart(' ', ',');
        }

        return string.IsNullOrWhiteSpace(normalized)
            ? SystemPrompt
            : $"{SystemPrompt},{normalized}";
    }
}
