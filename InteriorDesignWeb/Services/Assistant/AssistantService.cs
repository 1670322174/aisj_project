using System.Text.Json;
using InteriorDesignWeb.Application.Common;
using InteriorDesignWeb.Config;
using InteriorDesignWeb.Data;
using InteriorDesignWeb.Models.DTOs.AI;
using InteriorDesignWeb.Models.DTOs.Assistant;
using InteriorDesignWeb.Models.Entities;
using InteriorDesignWeb.Services.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InteriorDesignWeb.Services.Assistant;

public sealed class AssistantService : IAssistantService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly DesignHubContext _context;
    private readonly IAssistantModelClient _modelClient;
    private readonly IAIGenerationService _generationService;
    private readonly IWorkflowRegistry _workflowRegistry;
    private readonly IUsageQuotaService _usageQuotaService;
    private readonly AssistantOptions _options;
    private readonly ILogger<AssistantService> _logger;

    public AssistantService(
        DesignHubContext context,
        IAssistantModelClient modelClient,
        IAIGenerationService generationService,
        IWorkflowRegistry workflowRegistry,
        IUsageQuotaService usageQuotaService,
        IOptions<AssistantOptions> options,
        ILogger<AssistantService> logger)
    {
        _context = context;
        _modelClient = modelClient;
        _generationService = generationService;
        _workflowRegistry = workflowRegistry;
        _usageQuotaService = usageQuotaService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AssistantConversationDetailDto> CreateConversationAsync(
        int userId,
        CreateAssistantConversationRequest request,
        CancellationToken cancellationToken)
    {
        await ValidateBindingAsync(userId, request.ProjectId, request.RoomId, cancellationToken);
        var now = DateTime.UtcNow;
        var conversation = new AssistantConversation
        {
            UserID = userId,
            ProjectID = request.ProjectId,
            RoomID = request.RoomId,
            Title = string.IsNullOrWhiteSpace(request.Title) ? "新设计对话" : request.Title.Trim(),
            CurrentBriefJson = JsonSerializer.Serialize(new AssistantBriefDto(), JsonOptions),
            CreatedAt = now,
            UpdatedAt = now
        };
        _context.assistantconversations.Add(conversation);
        await _context.SaveChangesAsync(cancellationToken);
        return await GetConversationAsync(userId, conversation.ConversationID, cancellationToken);
    }

    public async Task<IReadOnlyList<AssistantConversationSummaryDto>> GetConversationsAsync(
        int userId,
        CancellationToken cancellationToken) =>
        await _context.assistantconversations.AsNoTracking()
            .Where(item => item.UserID == userId && !item.IsDeleted)
            .OrderByDescending(item => item.UpdatedAt)
            .Take(100)
            .Select(item => new AssistantConversationSummaryDto
            {
                ConversationId = item.ConversationID,
                Title = item.Title,
                Status = item.Status,
                ProjectId = item.ProjectID,
                ProjectName = item.Project != null ? item.Project.Name : null,
                RoomId = item.RoomID,
                RoomName = item.Room != null ? item.Room.Name : null,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            })
            .ToListAsync(cancellationToken);

    public async Task<AssistantConversationDetailDto> GetConversationAsync(
        int userId,
        long conversationId,
        CancellationToken cancellationToken)
    {
        var conversation = await GetConversationEntityAsync(userId, conversationId, cancellationToken);
        var messages = await _context.assistantmessages.AsNoTracking()
            .Where(item => item.ConversationID == conversationId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(100)
            .OrderBy(item => item.CreatedAt)
            .Select(item => new AssistantMessageDto
            {
                MessageId = item.MessageID,
                Role = item.Role,
                Content = item.Content,
                StructuredDataJson = item.StructuredDataJson,
                ModelCode = item.ModelCode,
                CreatedAt = item.CreatedAt
            })
            .ToListAsync(cancellationToken);
        var actions = await _context.assistantgenerationactions.AsNoTracking()
            .Where(item => item.ConversationID == conversationId)
            .OrderBy(item => item.CreatedAt)
            .Select(item => new AssistantActionDto
            {
                ActionId = item.ActionID,
                MessageId = item.MessageID,
                JobId = item.JobID,
                Status = item.Status,
                GenerationType = item.GenerationType,
                WorkflowCode = item.WorkflowCode,
                Prompt = item.Prompt,
                NegativePrompt = item.NegativePrompt,
                ParametersJson = item.ParametersJson,
                ProjectId = item.ProjectID,
                RoomId = item.RoomID,
                AutoAddToProject = item.AutoAddToProject,
                CreatedAt = item.CreatedAt
            })
            .ToListAsync(cancellationToken);
        return new AssistantConversationDetailDto
        {
            Conversation = await ToSummaryAsync(conversation, cancellationToken),
            Brief = DeserializeBrief(conversation.CurrentBriefJson),
            Messages = messages,
            Actions = actions
        };
    }

    public async Task<AssistantConversationSummaryDto> UpdateBindingAsync(
        int userId,
        long conversationId,
        UpdateAssistantBindingRequest request,
        CancellationToken cancellationToken)
    {
        await ValidateBindingAsync(userId, request.ProjectId, request.RoomId, cancellationToken);
        var conversation = await GetConversationEntityAsync(userId, conversationId, cancellationToken);
        conversation.ProjectID = request.ProjectId;
        conversation.RoomID = request.RoomId;
        conversation.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return await ToSummaryAsync(conversation, cancellationToken);
    }

    public async Task<AssistantChatResponseDto> SendMessageAsync(
        int userId,
        long conversationId,
        SendAssistantMessageRequest request,
        CancellationToken cancellationToken)
    {
        var conversation = await GetConversationEntityAsync(userId, conversationId, cancellationToken);
        var content = request.Content.Trim();
        var requestId = request.ClientRequestId.Trim();

        var existingUserMessage = await _context.assistantmessages.AsNoTracking()
            .FirstOrDefaultAsync(item => item.ConversationID == conversationId
                && item.ClientRequestID == requestId, cancellationToken);
        if (existingUserMessage != null)
        {
            var existingAssistant = await _context.assistantmessages.AsNoTracking()
                .Where(item => item.ConversationID == conversationId
                    && item.Role == "assistant"
                    && item.CreatedAt >= existingUserMessage.CreatedAt)
                .OrderBy(item => item.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (existingAssistant != null)
            {
                var existingAction = await _context.assistantgenerationactions.AsNoTracking()
                    .FirstOrDefaultAsync(item => item.MessageID == existingAssistant.MessageID, cancellationToken);
                return new AssistantChatResponseDto
                {
                    Message = ToMessageDto(existingAssistant),
                    Brief = DeserializeBrief(conversation.CurrentBriefJson),
                    ProposedAction = existingAction == null ? null : ToActionDto(existingAction)
                };
            }
        }

        var userMessage = new AssistantMessage
        {
            ConversationID = conversationId,
            Role = "user",
            Content = content,
            ClientRequestID = requestId,
            CreatedAt = DateTime.UtcNow
        };
        _context.assistantmessages.Add(userMessage);
        if (conversation.Title == "新设计对话") conversation.Title = Truncate(content, 36);
        conversation.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var contextMessages = await _context.assistantmessages.AsNoTracking()
            .Where(item => item.ConversationID == conversationId
                && (item.Role == "user" || item.Role == "assistant"))
            .OrderByDescending(item => item.CreatedAt)
            .Take(Math.Clamp(_options.MaxContextMessages, 4, 40))
            .OrderBy(item => item.CreatedAt)
            .Select(item => new AssistantModelMessage(item.Role, item.Content))
            .ToListAsync(cancellationToken);

        AssistantModelResult modelResult;
        AssistantTokenReservation? tokenReservation = null;
        try
        {
            var estimatedTokens = EstimateAssistantTokens(contextMessages, _options.MaxOutputTokens);
            tokenReservation = await _usageQuotaService.ReserveAssistantTokensAsync(
                userId,
                estimatedTokens,
                cancellationToken);
            modelResult = await _modelClient.CompleteAsync(
                DeserializeBrief(conversation.CurrentBriefJson),
                contextMessages,
                cancellationToken);
            var actualTokens = modelResult.InputTokens + modelResult.OutputTokens;
            await _usageQuotaService.SettleAssistantTokensAsync(
                userId,
                tokenReservation.ReservedTokens,
                actualTokens,
                cancellationToken);
        }
        catch (Exception ex)
        {
            if (tokenReservation != null)
            {
                await _usageQuotaService.ReleaseAssistantTokensAsync(
                    userId,
                    tokenReservation.ReservedTokens,
                    cancellationToken);
            }
            userMessage.ErrorMessage = Truncate(ex.Message, 500);
            await _context.SaveChangesAsync(cancellationToken);
            throw;
        }

        var structuredJson = JsonSerializer.Serialize(modelResult.Output, JsonOptions);
        var assistantMessage = new AssistantMessage
        {
            ConversationID = conversationId,
            Role = "assistant",
            Content = modelResult.Output.AssistantText,
            StructuredDataJson = structuredJson,
            ModelCode = Truncate(modelResult.Model, 100),
            InputTokens = modelResult.InputTokens,
            OutputTokens = modelResult.OutputTokens,
            DurationMs = modelResult.DurationMs,
            CreatedAt = DateTime.UtcNow
        };
        _context.assistantmessages.Add(assistantMessage);
        conversation.CurrentBriefJson = JsonSerializer.Serialize(modelResult.Output.Brief, JsonOptions);
        conversation.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        AssistantGenerationAction? action = null;
        var draft = modelResult.Output.GenerationDraft;
        if (modelResult.Output.Action == "propose_generation" && draft != null)
        {
            action = new AssistantGenerationAction
            {
                ConversationID = conversationId,
                MessageID = assistantMessage.MessageID,
                ProjectID = conversation.ProjectID,
                RoomID = conversation.RoomID,
                GenerationType = "text_to_image",
                Prompt = draft.Prompt,
                NegativePrompt = draft.NegativePrompt,
                ParametersJson = JsonSerializer.Serialize(draft.Parameters, JsonOptions),
                Status = "proposed",
                IdempotencyKey = Guid.NewGuid().ToString("N"),
                AutoAddToProject = true,
                CreatedAt = DateTime.UtcNow
            };
            _context.assistantgenerationactions.Add(action);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new AssistantChatResponseDto
        {
            Message = ToMessageDto(assistantMessage),
            Brief = modelResult.Output.Brief,
            ProposedAction = action == null ? null : ToActionDto(action)
        };
    }

    public async Task<AssistantGenerationResponseDto> ExecuteGenerationAsync(
        int userId,
        long conversationId,
        long actionId,
        ExecuteAssistantGenerationRequest request,
        CancellationToken cancellationToken)
    {
        var conversation = await GetConversationEntityAsync(userId, conversationId, cancellationToken);
        var action = await _context.assistantgenerationactions.FirstOrDefaultAsync(
            item => item.ActionID == actionId && item.ConversationID == conversationId,
            cancellationToken) ?? throw AppException.NotFound("生成建议不存在");

        if (!string.IsNullOrWhiteSpace(action.JobID))
        {
            return new AssistantGenerationResponseDto
            {
                ActionId = action.ActionID,
                JobId = action.JobID,
                Status = action.Status,
                WorkflowCode = action.WorkflowCode ?? string.Empty
            };
        }

        if (conversation.ProjectID == null || conversation.RoomID == null)
        {
            throw AppException.Validation("生成前请先为对话选择方案和房间。");
        }
        await ValidateBindingAsync(userId, conversation.ProjectID, conversation.RoomID, cancellationToken);

        var workflow = _workflowRegistry.GetAll().FirstOrDefault(item =>
            item.Enabled
            && item.OutputType.Equals("image", StringComparison.OrdinalIgnoreCase)
            && item.RequiredInputs.Count == 0)
            ?? throw new AppException(ErrorCodes.AssistantUnavailable, "当前没有可用的文生图工作流。", StatusCodes.Status503ServiceUnavailable);

        var prompt = string.IsNullOrWhiteSpace(request.Prompt) ? action.Prompt : request.Prompt.Trim();
        var negativePrompt = request.NegativePrompt ?? action.NegativePrompt;
        var parameters = request.Parameters ?? DeserializeParameters(action.ParametersJson);
        action.ProjectID = conversation.ProjectID;
        action.RoomID = conversation.RoomID;
        action.WorkflowCode = workflow.WorkflowCode;
        action.Prompt = prompt;
        action.NegativePrompt = negativePrompt;
        action.ParametersJson = JsonSerializer.Serialize(parameters, JsonOptions);
        action.AutoAddToProject = request.AutoAddToProject;
        action.Status = "submitting";
        action.ErrorMessage = null;
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            var result = await _generationService.SubmitAsync(new AIGenerationSubmitRequest
            {
                WorkflowCode = workflow.WorkflowCode,
                Prompt = prompt,
                NegativePrompt = negativePrompt,
                ProjectId = conversation.ProjectID,
                RoomId = conversation.RoomID,
                AutoAddToProject = request.AutoAddToProject,
                Parameters = parameters
            }, userId, cancellationToken, $"assistant-action:{action.ActionID}");

            action.JobID = result.JobId;
            action.Status = "submitted";
            action.ExecutedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return new AssistantGenerationResponseDto
            {
                ActionId = action.ActionID,
                JobId = result.JobId,
                Status = result.Status,
                WorkflowCode = result.WorkflowCode
            };
        }
        catch (Exception ex)
        {
            action.Status = "failed";
            action.ErrorMessage = Truncate(ex.Message, 500);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogWarning(ex, "助手生成建议提交失败. ActionId={ActionId}", action.ActionID);
            throw;
        }
    }

    public async Task DeleteConversationAsync(int userId, long conversationId, CancellationToken cancellationToken)
    {
        var conversation = await GetConversationEntityAsync(userId, conversationId, cancellationToken);
        conversation.IsDeleted = true;
        conversation.DeletedAt = DateTime.UtcNow;
        conversation.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<AssistantConversation> GetConversationEntityAsync(int userId, long conversationId, CancellationToken cancellationToken) =>
        await _context.assistantconversations.FirstOrDefaultAsync(
            item => item.ConversationID == conversationId && item.UserID == userId && !item.IsDeleted,
            cancellationToken) ?? throw AppException.NotFound("设计助手对话不存在");

    private async Task ValidateBindingAsync(int userId, int? projectId, int? roomId, CancellationToken cancellationToken)
    {
        if (projectId == null)
        {
            if (roomId != null) throw AppException.Validation("未选择方案时不能单独选择房间。");
            return;
        }
        var projectExists = await _context.projects.AsNoTracking().AnyAsync(
            item => item.ProjectID == projectId && item.UserID == userId && !item.IsDeleted,
            cancellationToken);
        if (!projectExists) throw AppException.Forbidden("方案不存在或不属于当前用户。");
        if (roomId != null)
        {
            var roomExists = await _context.projectrooms.AsNoTracking().AnyAsync(
                item => item.RoomID == roomId && item.ProjectID == projectId,
                cancellationToken);
            if (!roomExists) throw AppException.Validation("房间不存在或不属于所选方案。");
        }
    }

    private async Task<AssistantConversationSummaryDto> ToSummaryAsync(AssistantConversation item, CancellationToken cancellationToken)
    {
        var projectName = item.ProjectID == null ? null : await _context.projects.AsNoTracking()
            .Where(project => project.ProjectID == item.ProjectID)
            .Select(project => project.Name)
            .FirstOrDefaultAsync(cancellationToken);
        var roomName = item.RoomID == null ? null : await _context.projectrooms.AsNoTracking()
            .Where(room => room.RoomID == item.RoomID)
            .Select(room => room.Name)
            .FirstOrDefaultAsync(cancellationToken);
        return new AssistantConversationSummaryDto
        {
            ConversationId = item.ConversationID,
            Title = item.Title,
            Status = item.Status,
            ProjectId = item.ProjectID,
            ProjectName = projectName,
            RoomId = item.RoomID,
            RoomName = roomName,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };
    }

    private static AssistantMessageDto ToMessageDto(AssistantMessage item) => new()
    {
        MessageId = item.MessageID,
        Role = item.Role,
        Content = item.Content,
        StructuredDataJson = item.StructuredDataJson,
        ModelCode = item.ModelCode,
        CreatedAt = item.CreatedAt
    };

    private static AssistantActionDto ToActionDto(AssistantGenerationAction item) => new()
    {
        ActionId = item.ActionID,
        MessageId = item.MessageID,
        JobId = item.JobID,
        Status = item.Status,
        GenerationType = item.GenerationType,
        WorkflowCode = item.WorkflowCode,
        Prompt = item.Prompt,
        NegativePrompt = item.NegativePrompt,
        ParametersJson = item.ParametersJson,
        ProjectId = item.ProjectID,
        RoomId = item.RoomID,
        AutoAddToProject = item.AutoAddToProject,
        CreatedAt = item.CreatedAt
    };

    private static AssistantBriefDto DeserializeBrief(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new AssistantBriefDto();
        try { return JsonSerializer.Deserialize<AssistantBriefDto>(json, JsonOptions) ?? new(); }
        catch (JsonException) { return new AssistantBriefDto(); }
    }

    private static Dictionary<string, object?> DeserializeParameters(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<Dictionary<string, object?>>(json, JsonOptions) ?? new(); }
        catch (JsonException) { return new(); }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static int EstimateAssistantTokens(
        IReadOnlyCollection<AssistantModelMessage> messages,
        int maxOutputTokens)
    {
        // 中文通常接近一字一 token；再为系统规则、方案摘要和消息结构预留空间。
        var contextCharacters = messages.Sum(item => item.Content.Length);
        var inputEstimate = contextCharacters + 1200;
        return Math.Max(1, inputEstimate + Math.Clamp(maxOutputTokens, 256, 8000));
    }
}
