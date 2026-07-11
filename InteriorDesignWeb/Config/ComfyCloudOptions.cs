// 作用：集中定义 Comfy Cloud API 配置。
// ApiKey 必须通过本地配置、环境变量或密钥管理注入，禁止写入仓库和日志。

namespace InteriorDesignWeb.Config;

public sealed class ComfyCloudOptions
{
    public const string SectionName = "ComfyCloud";

    public string BaseUrl { get; set; } = "https://cloud.comfy.org/";

    public string ApiKey { get; set; } = string.Empty;

    public int RequestTimeoutMinutes { get; set; } = 15;

    public int PollIntervalSeconds { get; set; } = 3;

    public int MaxPollAttempts { get; set; } = 600;
}
