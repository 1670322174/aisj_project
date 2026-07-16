namespace InteriorDesignWeb.Config;

public sealed class AgentPlatformOptions
{
    public const string SectionName = "AgentPlatform";

    public bool Enabled { get; set; }
    public string ConfigurationRoot { get; set; } = "AIAgent";
    public bool StrictValidation { get; set; } = true;
    public bool FallbackToLegacy { get; set; } = true;
    public int MaxModelCallsPerRun { get; set; } = 5;
}
