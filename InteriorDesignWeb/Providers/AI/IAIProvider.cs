// 作用：定义 AI Provider 抽象，避免业务层直接依赖 ComfyUI API 细节。
// 后续可在不改 Controller 的情况下增加 CloudModelProvider 或 MockAIProvider。

using Newtonsoft.Json.Linq;
using Microsoft.AspNetCore.Http;

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
    public bool IsCompleted { get; set; }

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
