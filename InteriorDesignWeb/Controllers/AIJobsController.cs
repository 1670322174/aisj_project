// 作用：提供 AI 任务查询、结果查询和取消接口。
// 本 Controller 不创建占位任务，也不保留旧路由；所有真实任务统一由 POST /api/ai/generations 创建。

using System.Security.Claims;
using InteriorDesignWeb.Application.Common;
using InteriorDesignWeb.Data;
using InteriorDesignWeb.Models.DTOs.AI;
using InteriorDesignWeb.Services;
using InteriorDesignWeb.Services.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InteriorDesignWeb.Controllers;

[ApiController]
[Authorize]
[Route("api/ai/jobs")]
public class AIJobsController : ControllerBase
{
    private readonly IAIJobService _aiJobService;
    private readonly IAIGenerationService _generationService;
    private readonly DesignHubContext _context;
    private readonly CosService _cosService;

    public AIJobsController(
        IAIJobService aiJobService,
        IAIGenerationService generationService,
        DesignHubContext context,
        CosService cosService)
    {
        _aiJobService = aiJobService;
        _generationService = generationService;
        _context = context;
        _cosService = cosService;
    }

    /// <summary>
    /// 查询当前用户的 AI 任务历史。
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<AIJobDto>>>> GetMyJobs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _aiJobService.GetUserJobsAsync(
            GetCurrentUserId(),
            page,
            pageSize,
            cancellationToken);

        return Ok(ApiResponse<PagedResult<AIJobDto>>.Ok(
            result,
            "查询成功",
            HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 查询单个 AI 任务状态、进度和错误信息。
    /// </summary>
    [HttpGet("{jobId}")]
    public async Task<ActionResult<ApiResponse<AIJobDto>>> GetJob(
        string jobId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        await _generationService.RefreshAsync(jobId, userId, cancellationToken);

        var job = await _aiJobService.GetJobAsync(
            jobId,
            userId,
            cancellationToken);

        if (job == null)
        {
            throw new AppException(
                ErrorCodes.AiJobNotFound,
                "AI任务不存在或无权访问。",
                StatusCodes.Status404NotFound);
        }

        return Ok(ApiResponse<AIJobDto>.Ok(
            job,
            "查询成功",
            HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 查询已经下载到 COS 并写入数据库的图片或视频结果。
    /// </summary>
    [HttpGet("{jobId}/results")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AIJobResultDto>>>> GetJobResults(
        string jobId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        await _generationService.RefreshAsync(jobId, userId, cancellationToken);

        var results = await _aiJobService.GetJobResultsAsync(
            jobId,
            userId,
            cancellationToken);

        foreach (var result in results)
        {
            result.ThumbnailUrl = Url.Action(
                nameof(GetJobResultFile),
                new { jobId, aiImageId = result.AiImageID, type = "thumbnail" });
            result.OriginalUrl = Url.Action(
                nameof(GetJobResultFile),
                new { jobId, aiImageId = result.AiImageID, type = "original" });
        }

        return Ok(ApiResponse<IReadOnlyList<AIJobResultDto>>.Ok(
            results,
            "查询成功",
            HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 读取当前用户 AI 任务的结果文件。通过后端代理 COS，避免前端拼接对象路径。
    /// </summary>
    [HttpGet("{jobId}/results/{aiImageId:int}/file")]
    public async Task<IActionResult> GetJobResultFile(
        string jobId,
        int aiImageId,
        [FromQuery] string type = "original",
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var canAccess = await _context.aigenerationjobimages
            .AsNoTracking()
            .AnyAsync(
                image => image.AiImageID == aiImageId
                    && image.JobId == jobId
                    && (image.UserID == userId
                        || (image.AiGenerationJob != null
                            && image.AiGenerationJob.UserID == userId)),
                cancellationToken);

        if (!canAccess)
        {
            throw new AppException(
                ErrorCodes.AiJobNotFound,
                "AI结果不存在或无权访问。",
                StatusCodes.Status404NotFound);
        }

        var normalizedType = type.Equals("thumbnail", StringComparison.OrdinalIgnoreCase)
            ? "thumbnail"
            : "original";
        var (stream, contentType) = await _cosService.GetImageStreamByAiImageIdAsync(
            aiImageId,
            normalizedType);

        return File(stream, contentType, enableRangeProcessing: true);
    }

    /// <summary>
    /// 取消排队中或运行中的 ComfyUI Server 任务。
    /// </summary>
    [HttpPost("{jobId}/cancel")]
    public async Task<ActionResult<ApiResponse<object>>> Cancel(
        string jobId,
        CancellationToken cancellationToken)
    {
        var cancelled = await _generationService.CancelAsync(
            jobId,
            GetCurrentUserId(),
            cancellationToken);

        return Ok(ApiResponse<object>.Ok(
            new { cancelled },
            cancelled ? "任务已取消" : "任务当前无法取消",
            HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 从任务历史中软删除一条终态任务记录，不删除生成结果或方案图片。
    /// </summary>
    [HttpDelete("{jobId}")]
    public async Task<IActionResult> DeleteJob(
        string jobId,
        CancellationToken cancellationToken)
    {
        await _aiJobService.DeleteJobAsync(
            jobId,
            GetCurrentUserId(),
            cancellationToken);

        return NoContent();
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
