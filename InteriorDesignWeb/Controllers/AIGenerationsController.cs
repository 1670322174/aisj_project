// 作用：提供统一 AI 生图业务接口。
// 本 Controller 面向 7 个工作流接入：上传 ComfyUI 输入图、查询工作流选项、提交生成任务、取消任务。

using System.Security.Claims;
using InteriorDesignWeb.Application.Common;
using InteriorDesignWeb.Models.DTOs.AI;
using InteriorDesignWeb.Services.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace InteriorDesignWeb.Controllers;

[ApiController]
[Authorize]
[Route("api/ai/generations")]
public class AIGenerationsController : ControllerBase
{
    private readonly IAIGenerationService _generationService;

    public AIGenerationsController(IAIGenerationService generationService)
    {
        _generationService = generationService;
    }

    /// <summary>
    /// 获取当前可用的 AI 工作流列表。
    /// </summary>
    [HttpGet("options")]
    public ActionResult<ApiResponse<IReadOnlyList<WorkflowOptionDto>>> GetOptions()
    {
        var options = _generationService.GetWorkflowOptions();
        return Ok(ApiResponse<IReadOnlyList<WorkflowOptionDto>>.Ok(
            options,
            "查询成功",
            HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 上传图片到 ComfyUI input 目录，返回工作流可使用的文件名。
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
    /// 提交 AI 生成任务。
    /// 支持 workflow 目录中的 7 个工作流，前端只需要传 workflowCode 和对应参数。
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<AIGenerationSubmitResponse>>> Submit(
        [FromBody] AIGenerationSubmitRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _generationService.SubmitAsync(request, userId, cancellationToken);

        return Ok(ApiResponse<AIGenerationSubmitResponse>.Ok(
            result,
            "AI任务已提交",
            HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 取消 AI 生成任务。
    /// </summary>
    [HttpPost("{jobId}/cancel")]
    public async Task<ActionResult<ApiResponse<object>>> Cancel(
        string jobId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var cancelled = await _generationService.CancelAsync(jobId, userId, cancellationToken);

        return Ok(ApiResponse<object>.Ok(
            new { cancelled },
            cancelled ? "任务已取消" : "取消任务失败",
            HttpContext.TraceIdentifier));
    }

    private int GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userIdValue) || !int.TryParse(userIdValue, out var userId))
        {
            throw new AppException(
                ErrorCodes.Unauthorized,
                "登录状态无效，请重新登录。",
                StatusCodes.Status401Unauthorized);
        }

        return userId;
    }
}
