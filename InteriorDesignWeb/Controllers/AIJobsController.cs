// 作用：提供 AI 生成任务中心的对外 API。
// 当前阶段只负责创建任务、查询任务列表、查询任务详情；
// 不直接调用 ComfyUI，也不接入 7 个具体工作流。

using System.Security.Claims;
using InteriorDesignWeb.Application.Common;
using InteriorDesignWeb.Models.DTOs.AI;
using InteriorDesignWeb.Services.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InteriorDesignWeb.Controllers;

[ApiController]
[Authorize]
[Route("api/ai-jobs")]
public class AIJobsController : ControllerBase
{
    private readonly IAIJobService _aiJobService;
    private readonly ILogger<AIJobsController> _logger;

    public AIJobsController(
        IAIJobService aiJobService,
        ILogger<AIJobsController> logger)
    {
        _aiJobService = aiJobService;
        _logger = logger;
    }

    /// <summary>
    /// 创建 AI 生成任务。
    /// 当前阶段只创建任务记录，不提交到 ComfyUI。
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<AIJobDto>>> CreateJob(
        [FromBody] CreateAIJobRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        var job = await _aiJobService.CreateJobAsync(
            request,
            userId,
            cancellationToken);

        _logger.LogInformation(
            "用户创建 AI 任务。UserID={UserID}, JobId={JobId}, WorkflowCode={WorkflowCode}",
            userId,
            job.JobId,
            job.WorkflowCode);

        return Ok(ApiResponse<AIJobDto>.Ok(
            job,
            "AI任务已创建",
            HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 查询当前用户的 AI 任务列表。
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<AIJobDto>>>> GetMyJobs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        var result = await _aiJobService.GetUserJobsAsync(
            userId,
            page,
            pageSize,
            cancellationToken);

        return Ok(ApiResponse<PagedResult<AIJobDto>>.Ok(
            result,
            "查询成功",
            HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 查询当前用户的单个 AI 任务详情。
    /// </summary>
    [HttpGet("{jobId}")]
    public async Task<ActionResult<ApiResponse<AIJobDto>>> GetJob(
        string jobId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

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
    /// 从 JWT Claims 中读取当前用户 ID。
    /// </summary>
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
