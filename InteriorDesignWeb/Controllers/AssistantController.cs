using System.Security.Claims;
using InteriorDesignWeb.Application.Common;
using InteriorDesignWeb.Models.DTOs.Assistant;
using InteriorDesignWeb.Services.Assistant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InteriorDesignWeb.Controllers;

[ApiController]
[Authorize]
[Route("api/assistant")]
public sealed class AssistantController : ControllerBase
{
    private readonly IAssistantService _assistantService;
    private readonly IAssistantAttachmentService _attachmentService;

    public AssistantController(
        IAssistantService assistantService,
        IAssistantAttachmentService attachmentService)
    {
        _assistantService = assistantService;
        _attachmentService = attachmentService;
    }

    [HttpPost("conversations")]
    public async Task<ActionResult<ApiResponse<AssistantConversationDetailDto>>> CreateConversation(
        [FromBody] CreateAssistantConversationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _assistantService.CreateConversationAsync(GetUserId(), request, cancellationToken);
        return Ok(ApiResponse<AssistantConversationDetailDto>.Ok(result, "对话已创建", HttpContext.TraceIdentifier));
    }

    [HttpGet("conversations")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AssistantConversationSummaryDto>>>> GetConversations(CancellationToken cancellationToken)
    {
        var result = await _assistantService.GetConversationsAsync(GetUserId(), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AssistantConversationSummaryDto>>.Ok(result, "查询成功", HttpContext.TraceIdentifier));
    }

    [HttpGet("conversations/{conversationId:long}")]
    public async Task<ActionResult<ApiResponse<AssistantConversationDetailDto>>> GetConversation(long conversationId, CancellationToken cancellationToken)
    {
        var result = await _assistantService.GetConversationAsync(GetUserId(), conversationId, cancellationToken);
        return Ok(ApiResponse<AssistantConversationDetailDto>.Ok(result, "查询成功", HttpContext.TraceIdentifier));
    }

    [HttpPatch("conversations/{conversationId:long}/binding")]
    public async Task<ActionResult<ApiResponse<AssistantConversationSummaryDto>>> UpdateBinding(
        long conversationId,
        [FromBody] UpdateAssistantBindingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _assistantService.UpdateBindingAsync(GetUserId(), conversationId, request, cancellationToken);
        return Ok(ApiResponse<AssistantConversationSummaryDto>.Ok(result, "方案绑定已更新", HttpContext.TraceIdentifier));
    }

    [HttpPost("conversations/{conversationId:long}/messages")]
    [EnableRateLimiting("assistant")]
    public async Task<ActionResult<ApiResponse<AssistantChatResponseDto>>> SendMessage(
        long conversationId,
        [FromBody] SendAssistantMessageRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _assistantService.SendMessageAsync(GetUserId(), conversationId, request, cancellationToken);
        return Ok(ApiResponse<AssistantChatResponseDto>.Ok(result, "回复成功", HttpContext.TraceIdentifier));
    }

    [HttpPost("chat")]
    [EnableRateLimiting("assistant")]
    public Task<ActionResult<ApiResponse<AssistantChatResponseDto>>> Chat(
        [FromBody] AssistantChatRequest request,
        CancellationToken cancellationToken) =>
        SendMessage(request.ConversationId, new SendAssistantMessageRequest
        {
            Content = request.Content,
            ClientRequestId = request.ClientRequestId,
            AttachmentIds = request.AttachmentIds
        }, cancellationToken);

    [HttpPost("conversations/{conversationId:long}/attachments")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(16 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<AssistantAttachmentDto>>> UploadAttachment(
        long conversationId,
        [FromForm] UploadAssistantAttachmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _attachmentService.UploadAsync(
            GetUserId(), conversationId, request.RoomId, request.File, cancellationToken);
        return Ok(ApiResponse<AssistantAttachmentDto>.Ok(
            result, "图片已上传，发送消息后将由视觉 Agent 分析。", HttpContext.TraceIdentifier));
    }

    [HttpGet("conversations/{conversationId:long}/attachments/{attachmentId:long}/file")]
    public async Task<IActionResult> GetAttachmentFile(
        long conversationId,
        long attachmentId,
        [FromQuery] string type = "thumbnail",
        CancellationToken cancellationToken = default)
    {
        var result = await _attachmentService.OpenAsync(
            GetUserId(), conversationId, attachmentId,
            type.Equals("thumbnail", StringComparison.OrdinalIgnoreCase),
            cancellationToken);
        return File(result.Stream, result.ContentType, enableRangeProcessing: true);
    }

    [HttpDelete("conversations/{conversationId:long}/attachments/{attachmentId:long}")]
    public async Task<IActionResult> DeleteAttachment(
        long conversationId,
        long attachmentId,
        CancellationToken cancellationToken)
    {
        await _attachmentService.DeleteAsync(
            GetUserId(), conversationId, attachmentId, cancellationToken);
        return NoContent();
    }

    [HttpPost("conversations/{conversationId:long}/actions/{actionId:long}/execute")]
    public async Task<ActionResult<ApiResponse<AssistantGenerationResponseDto>>> ExecuteGeneration(
        long conversationId,
        long actionId,
        [FromBody] ExecuteAssistantGenerationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _assistantService.ExecuteGenerationAsync(GetUserId(), conversationId, actionId, request, cancellationToken);
        return Ok(ApiResponse<AssistantGenerationResponseDto>.Ok(result, "AI 任务已提交", HttpContext.TraceIdentifier));
    }

    [HttpPost("conversations/{conversationId:long}/actions/{actionId:long}/evaluate")]
    [EnableRateLimiting("assistant")]
    public async Task<ActionResult<ApiResponse<AssistantResultEvaluationDto>>> EvaluateGeneration(
        long conversationId,
        long actionId,
        [FromBody] EvaluateAssistantResultRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _assistantService.EvaluateGenerationAsync(
            GetUserId(), conversationId, actionId, request, cancellationToken);
        return Ok(ApiResponse<AssistantResultEvaluationDto>.Ok(
            result, "效果图评估完成", HttpContext.TraceIdentifier));
    }

    [HttpGet("conversations/{conversationId:long}/agent-runs/by-request/{clientRequestId}")]
    public async Task<ActionResult<ApiResponse<AssistantAgentRunDto>>> GetAgentRun(
        long conversationId,
        string clientRequestId,
        CancellationToken cancellationToken)
    {
        var result = await _assistantService.GetAgentRunAsync(
            GetUserId(),
            conversationId,
            clientRequestId,
            cancellationToken);
        return Ok(ApiResponse<AssistantAgentRunDto>.Ok(result, "查询成功", HttpContext.TraceIdentifier));
    }

    [HttpDelete("conversations/{conversationId:long}")]
    public async Task<IActionResult> DeleteConversation(long conversationId, CancellationToken cancellationToken)
    {
        await _assistantService.DeleteConversationAsync(GetUserId(), conversationId, cancellationToken);
        return NoContent();
    }

    private int GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(value, out var userId))
        {
            throw new AppException(ErrorCodes.Unauthorized, "登录状态无效，请重新登录。", StatusCodes.Status401Unauthorized);
        }
        return userId;
    }
}
