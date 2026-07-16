// 作用：提供统一 AI 生成入口。
// 本 Controller 只负责工作流选项、输入文件上传和提交任务；任务查询与取消统一由 AIJobsController 处理。

using System.Security.Claims;
using InteriorDesignWeb.Application.Common;
using InteriorDesignWeb.Models.DTOs.AI;
using InteriorDesignWeb.Services.AI;
using InteriorDesignWeb.Services.Assistant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InteriorDesignWeb.Controllers;

[ApiController]
[Authorize]
[Route("api/ai/generations")]
public class AIGenerationsController : ControllerBase
{
    private readonly IAIGenerationService _generationService;
    private readonly IAssistantGovernanceService _governanceService;

    public AIGenerationsController(
        IAIGenerationService generationService,
        IAssistantGovernanceService governanceService)
    {
        _generationService = generationService;
        _governanceService = governanceService;
    }

    /// <summary>
    /// 获取当前可用的 7 个 AI 工作流及其输入要求。
    /// </summary>
    [HttpGet("options")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<WorkflowOptionDto>>>> GetOptions(
        CancellationToken cancellationToken)
    {
        var policy = await _governanceService.GetEffectivePolicyAsync(GetCurrentUserId(), cancellationToken);
        var options = _generationService.GetWorkflowOptions()
            .Where(item => policy.CanExecuteGeneration
                && (policy.AllowedWorkflowCodes.Count == 0
                    || policy.AllowedWorkflowCodes.Contains(item.WorkflowCode)))
            .ToList();
        return Ok(ApiResponse<IReadOnlyList<WorkflowOptionDto>>.Ok(
            options,
            "查询成功",
            HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 上传工作流所需的输入图片到 ComfyUI Server。
    /// </summary>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<ComfyUploadResponseDto>>> UploadInputImage(
        IFormFile file,
        [FromForm] string fieldName = "sourceImage",
        [FromForm] string? subfolder = null,
        [FromForm] bool overwrite = true,
        CancellationToken cancellationToken = default)
    {
        var policy = await _governanceService.GetEffectivePolicyAsync(GetCurrentUserId(), cancellationToken);
        if (!policy.CanExecuteGeneration)
            throw AppException.Forbidden("管理员已禁止当前账号使用 AI 生图。");
        var result = await _generationService.UploadInputImageAsync(
            file,
            fieldName,
            subfolder,
            overwrite,
            cancellationToken);

        return Ok(ApiResponse<ComfyUploadResponseDto>.Ok(
            result,
            "上传成功",
            HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 构建指定工作流并提交到 ComfyUI Server，返回网站 JobId 和 ComfyUI prompt_id。
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<AIGenerationSubmitResponse>>> Submit(
        [FromBody] AIGenerationSubmitRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _generationService.SubmitAsync(
            request,
            userId,
            cancellationToken);

        return Ok(ApiResponse<AIGenerationSubmitResponse>.Ok(
            result,
            "AI任务已提交",
            HttpContext.TraceIdentifier));
    }

    private int GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userIdValue)
            || !int.TryParse(userIdValue, out var userId))
        {
            throw new AppException(
                ErrorCodes.Unauthorized,
                "登录状态无效，请重新登录。",
                StatusCodes.Status401Unauthorized);
        }

        return userId;
    }
}
