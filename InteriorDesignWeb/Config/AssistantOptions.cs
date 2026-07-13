namespace InteriorDesignWeb.Config;

public sealed class AssistantOptions
{
    public const string SectionName = "Assistant";
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 90;
    public int MaxContextMessages { get; set; } = 20;
    public int MaxOutputTokens { get; set; } = 2000;
    public bool UseJsonResponseFormat { get; set; } = true;
}
