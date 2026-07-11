// 作用：定义 AI 工作流执行 Provider 的统一接口。
// 当前唯一实现是 ComfyUIServerProvider，业务层无需了解 ComfyUI Server 的 HTTP 协议。

using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;

namespace InteriorDesignWeb.Providers.AI;

public interface IAIProvider
{
    string ProviderType { get; }

    Task<AIProviderSubmitResult> SubmitAsync(
        JObject workflow,
        CancellationToken cancellationToken = default);

    Task<AIProviderHistoryResult> GetHistoryAsync(
        string providerJobId,
        CancellationToken cancellationToken = default);

    Task<AIProviderUploadResult> UploadImageAsync(
        IFormFile file,
        string? subfolder = null,
        bool overwrite = true,
        CancellationToken cancellationToken = default);

    Task<Stream> DownloadOutputAsync(
        AIProviderOutput output,
        CancellationToken cancellationToken = default);

    Task<bool> CancelAsync(
        string providerJobId,
        CancellationToken cancellationToken = default);
}

public class AIProviderSubmitResult
{
    public string ProviderJobId { get; set; } = string.Empty;
}

public class AIProviderHistoryResult
{
    public string Status { get; set; } = "pending";

    public bool IsCompleted { get; set; }

    public bool IsFailed { get; set; }

    public bool IsCancelled { get; set; }

    public int ProgressValue { get; set; }

    public string? ErrorMessage { get; set; }

    public string RawJson { get; set; } = string.Empty;

    public IReadOnlyList<AIProviderOutput> Outputs { get; set; } = Array.Empty<AIProviderOutput>();
}

public class AIProviderOutput
{
    public string FileName { get; set; } = string.Empty;

    public string Subfolder { get; set; } = string.Empty;

    public string Type { get; set; } = "output";

    public string MediaType { get; set; } = "image";

    public string NodeId { get; set; } = string.Empty;
}

public class AIProviderUploadResult
{
    public string Name { get; set; } = string.Empty;

    public string Subfolder { get; set; } = string.Empty;

    public string Type { get; set; } = "input";
}
