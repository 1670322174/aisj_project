// 作用：定义 AI 结果保存服务接口。
// Provider 生成的图片或视频通过本服务统一上传 COS 并写入 aigenerationjobimages。

using InteriorDesignWeb.Models.DTOs.AI;
using InteriorDesignWeb.Providers.AI;

namespace InteriorDesignWeb.Services.AI;

public interface IAIResultService
{
    Task<IReadOnlyList<AIResultImageDto>> SaveProviderOutputsAsync(
        string jobId,
        int? userId,
        WorkflowDefinition workflow,
        AIGenerationSubmitRequest request,
        IReadOnlyList<AIProviderOutput> outputs,
        CancellationToken cancellationToken = default);
}

public class AIResultImageDto
{
    public int AiImageID { get; set; }

    public string ImageUrl { get; set; } = string.Empty;

    public string CosPath { get; set; } = string.Empty;

    public string? ThumbnailPath { get; set; }

    public string MediaType { get; set; } = "image";
}
