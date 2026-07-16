namespace InteriorDesignWeb.Config;

public static class AgentModelProviderIds
{
    public const string MiniMax = "minimax";
    public const string DeepSeek = "deepseek";
    public const string VolcArk = "volc-ark";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        [MiniMax, DeepSeek, VolcArk],
        StringComparer.OrdinalIgnoreCase);
}

public sealed class AgentModelsOptions
{
    public const string SectionName = "AgentModels";

    public Dictionary<string, AgentModelProfileOptions> Profiles { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class AgentModelProfileOptions
{
    public bool Enabled { get; set; } = true;
    public string Provider { get; set; } = string.Empty;
    public string Protocol { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 120;
    public int MaxOutputTokens { get; set; } = 4000;
    public string ThinkingMode { get; set; } = "disabled";
    public string ReasoningEffort { get; set; } = "high";
    public AgentModelCapabilitiesOptions Capabilities { get; set; } = new();
}

public sealed class AgentModelCapabilitiesOptions
{
    public bool Text { get; set; } = true;
    public bool Vision { get; set; }
    public bool Tools { get; set; }
    public bool JsonOutput { get; set; }
    public bool Thinking { get; set; }
}
