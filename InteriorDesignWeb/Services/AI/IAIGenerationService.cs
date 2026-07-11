// 作用：定义统一 AI 生图业务入口。
// 本接口负责提交 7 个工作流、上传 ComfyUI Server 输入图和查询可用工作流，不向前端暴露 ComfyUI 协议。

using InteriorDesignWeb.Models.DTOs.AI;
using Microsoft.AspNetCore.Http;

namespace InteriorDesignWeb.Services.AI;

public interface IAIGenerationService
{
    IReadOnlyList<WorkflowOptionDto> GetWorkflowOptions();

    Task<ComfyUploadResponseDto> UploadInputImageAsync(
        IFormFile file,
        string fieldName,
        string? subfolder = null,
        bool overwrite = true,
        CancellationToken cancellationToken = default);

    Task<AIGenerationSubmitResponse> SubmitAsync(
        AIGenerationSubmitRequest request,
        int userId,
        CancellationToken cancellationToken = default);

    Task RefreshAsync(
        string jobId,
        int userId,
        CancellationToken cancellationToken = default);

    Task<bool> CancelAsync(
        string jobId,
        int userId,
        CancellationToken cancellationToken = default);
}
