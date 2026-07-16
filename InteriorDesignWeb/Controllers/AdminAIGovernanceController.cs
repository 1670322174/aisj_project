using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using InteriorDesignWeb.Application.Common;
using InteriorDesignWeb.Config;
using InteriorDesignWeb.Data;
using InteriorDesignWeb.Models.DTOs.Admin;
using InteriorDesignWeb.Models.DTOs.Assistant;
using InteriorDesignWeb.Models.Entities;
using InteriorDesignWeb.Services.AI;
using InteriorDesignWeb.Services.Assistant;
using InteriorDesignWeb.Services.Assistant.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InteriorDesignWeb.Controllers;

[ApiController]
[Route("api/admin/ai-governance")]
[Authorize(Roles = nameof(UserRole.Administrator))]
public sealed class AdminAIGovernanceController : ControllerBase
{
    private readonly DesignHubContext _context;
    private readonly IAssistantModelClient _modelClient;
    private readonly IAssistantGovernanceService _governanceService;
    private readonly IWorkflowRegistry _workflowRegistry;
    private readonly AssistantOptions _assistantOptions;
    private readonly AgentPlatformOptions _agentPlatformOptions;
    private readonly IAgentModelRouter _agentModelRouter;
    private readonly IAgentConfigurationCatalog _agentConfigurationCatalog;

    public AdminAIGovernanceController(
        DesignHubContext context,
        IAssistantModelClient modelClient,
        IAssistantGovernanceService governanceService,
        IWorkflowRegistry workflowRegistry,
        IOptions<AssistantOptions> assistantOptions,
        IOptions<AgentPlatformOptions> agentPlatformOptions,
        IAgentModelRouter agentModelRouter,
        IAgentConfigurationCatalog agentConfigurationCatalog)
    {
        _context = context;
        _modelClient = modelClient;
        _governanceService = governanceService;
        _workflowRegistry = workflowRegistry;
        _assistantOptions = assistantOptions.Value;
        _agentPlatformOptions = agentPlatformOptions.Value;
        _agentModelRouter = agentModelRouter;
        _agentConfigurationCatalog = agentConfigurationCatalog;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var agentConfiguration = _agentConfigurationCatalog.Current;
        var versions = await _context.assistantpolicyversions.AsNoTracking()
            .OrderByDescending(item => item.VersionNumber)
            .Take(30)
            .Select(item => new
            {
                item.PolicyVersionID,
                item.VersionNumber,
                item.Name,
                item.BusinessPrompt,
                item.IsPublished,
                item.CreatedByUserID,
                item.CreatedAt,
                item.PublishedByUserID,
                item.PublishedAt
            })
            .ToListAsync(cancellationToken);
        var roles = await _context.airolepolicies.AsNoTracking()
            .OrderBy(item => item.Role)
            .ToListAsync(cancellationToken);
        var overrides = await _context.aiuserpolicyoverrides.AsNoTracking()
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .Take(100)
            .Join(_context.users.AsNoTracking(), item => item.UserID, user => user.UserID, (item, user) => new
            {
                item.UserPolicyOverrideID,
                item.UserID,
                Username = user.UserName,
                item.AssistantEnabled,
                item.CanProposeGeneration,
                item.CanExecuteGeneration,
                item.CanAutoAddToProject,
                item.MaxConcurrentJobs,
                item.AllowedWorkflowCodesJson,
                item.ExpiresAt,
                item.UpdatedAt
            })
            .ToListAsync(cancellationToken);
        var recentAgentRuns = await _context.assistantagentruns.AsNoTracking()
            .OrderByDescending(item => item.StartedAt)
            .Take(30)
            .Select(item => new
            {
                item.RunID,
                item.ConversationID,
                ConversationTitle = item.Conversation != null ? item.Conversation.Title : string.Empty,
                item.UserID,
                Username = item.Conversation != null && item.Conversation.User != null
                    ? item.Conversation.User.UserName
                    : string.Empty,
                item.ClientRequestID,
                item.Status,
                item.EntryAgentID,
                item.CurrentAgentID,
                item.CurrentStage,
                item.ModelCallCount,
                item.HandoffCount,
                item.InputTokens,
                item.OutputTokens,
                item.DurationMs,
                item.ErrorCode,
                item.ErrorMessage,
                item.StartedAt,
                item.CompletedAt,
                LatestEvent = item.Events.OrderByDescending(evt => evt.Sequence).Select(evt => evt.Title).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<object>.Ok(new
        {
            Provider = new
            {
                _assistantOptions.Enabled,
                Configured = _assistantOptions.Enabled
                    && !string.IsNullOrWhiteSpace(_assistantOptions.BaseUrl)
                    && !string.IsNullOrWhiteSpace(_assistantOptions.ApiKey)
                    && !string.IsNullOrWhiteSpace(_assistantOptions.Model),
                BaseUrl = GetSafeBaseUrl(_assistantOptions.BaseUrl),
                _assistantOptions.Model,
                ApiKeyConfigured = !string.IsNullOrWhiteSpace(_assistantOptions.ApiKey),
                _assistantOptions.TimeoutSeconds,
                _assistantOptions.MaxContextMessages,
                _assistantOptions.MaxOutputTokens,
                _assistantOptions.UseJsonResponseFormat,
                _assistantOptions.ResponseFormatMode,
                _assistantOptions.RepairInvalidStructuredOutput,
                _assistantOptions.AllowNaturalLanguageFallback
            },
            AgentPlatform = new
            {
                _agentPlatformOptions.Enabled,
                _agentPlatformOptions.ConfigurationRoot,
                _agentPlatformOptions.StrictValidation,
                _agentPlatformOptions.FallbackToLegacy,
                _agentPlatformOptions.MaxModelCallsPerRun,
                agentConfiguration.LoadedAt,
                agentConfiguration.Version,
                agentConfiguration.DefaultAgentId,
                agentConfiguration.MaxHandoffDepth,
                agentConfiguration.MaxAgentsPerRun,
                agentConfiguration.Valid,
                AgentCount = agentConfiguration.Agents.Count,
                ToolCount = agentConfiguration.ToolIds.Count,
                agentConfiguration.ValidationErrors,
                Agents = agentConfiguration.Agents.Select(item => new
                {
                    item.Id,
                    item.DisplayName,
                    item.Description,
                    item.Enabled,
                    item.Mode,
                    item.DefaultModelProfile,
                    item.AllowedModelProfiles,
                    item.SkillIds,
                    item.AllowedTools,
                    item.HandoffTargets,
                    item.PresentationModes,
                    item.MaxSteps,
                    item.MaxOutputTokens,
                    item.TimeoutSeconds
                })
            },
            AgentModels = _agentModelRouter.GetProfileStatuses(),
            RecentAgentRuns = recentAgentRuns,
            CorePolicy = new
            {
                Version = AssistantGovernanceService.CorePolicyVersion,
                Editable = false,
                Summary = "后端不可变安全规则：不信任用户数据、禁止泄密和权限提升、只允许结构化白名单动作。"
            },
            PolicyVersions = versions,
            RolePolicies = roles.Select(item => new
            {
                item.RolePolicyID,
                Role = item.Role.ToString(),
                item.AssistantEnabled,
                item.CanProposeGeneration,
                item.CanExecuteGeneration,
                item.CanAutoAddToProject,
                item.MaxConcurrentJobs,
                AllowedWorkflowCodes = AssistantGovernanceService.ParseWorkflowCodes(item.AllowedWorkflowCodesJson)
            }),
            UserOverrides = overrides.Select(item => new
            {
                item.UserPolicyOverrideID,
                item.UserID,
                item.Username,
                item.AssistantEnabled,
                item.CanProposeGeneration,
                item.CanExecuteGeneration,
                item.CanAutoAddToProject,
                item.MaxConcurrentJobs,
                AllowedWorkflowCodes = item.AllowedWorkflowCodesJson == null
                    ? null
                    : AssistantGovernanceService.ParseWorkflowCodes(item.AllowedWorkflowCodesJson),
                item.ExpiresAt,
                item.UpdatedAt
            }),
            Workflows = _workflowRegistry.GetAll().Where(item => item.Enabled).Select(item => new
            {
                item.WorkflowCode,
                item.Name,
                item.OutputType,
                item.CostUnits
            })
        }, "查询成功", HttpContext.TraceIdentifier));
    }

    [HttpGet("agent-runs/{runId:long}")]
    public async Task<IActionResult> GetAgentRun(long runId, CancellationToken cancellationToken)
    {
        var run = await _context.assistantagentruns.AsNoTracking()
            .Where(item => item.RunID == runId)
            .Select(item => new
            {
                item.RunID,
                item.ConversationID,
                ConversationTitle = item.Conversation != null ? item.Conversation.Title : string.Empty,
                item.UserID,
                Username = item.Conversation != null && item.Conversation.User != null
                    ? item.Conversation.User.UserName
                    : string.Empty,
                item.ClientRequestID,
                item.Status,
                item.EntryAgentID,
                item.CurrentAgentID,
                item.CurrentStage,
                item.ModelCallCount,
                item.HandoffCount,
                item.InputTokens,
                item.OutputTokens,
                item.DurationMs,
                item.ErrorCode,
                item.ErrorMessage,
                item.StartedAt,
                item.CompletedAt,
                Events = item.Events.OrderBy(evt => evt.Sequence).Select(evt => new
                {
                    evt.EventID,
                    evt.Sequence,
                    evt.AgentID,
                    evt.EventType,
                    evt.Stage,
                    evt.Title,
                    evt.Detail,
                    evt.DataJson,
                    evt.CreatedAt
                }),
                Artifacts = item.Artifacts.OrderBy(artifact => artifact.CreatedAt).Select(artifact => new
                {
                    artifact.ArtifactID,
                    artifact.AgentID,
                    artifact.ArtifactType,
                    artifact.Version,
                    artifact.Status,
                    artifact.Title,
                    artifact.CreatedAt,
                    ContentLength = artifact.ContentJson.Length
                })
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (run == null) throw AppException.NotFound("未找到 Agent 运行记录。");
        return Ok(ApiResponse<object>.Ok(run, "查询成功", HttpContext.TraceIdentifier));
    }

    [HttpPost("policy/drafts")]
    public async Task<IActionResult> CreateDraft(
        [FromBody] AdminCreateAssistantPolicyDraftRequest request,
        CancellationToken cancellationToken)
    {
        var nextVersion = (await _context.assistantpolicyversions.MaxAsync(
            item => (int?)item.VersionNumber, cancellationToken) ?? 0) + 1;
        var version = new AssistantPolicyVersion
        {
            VersionNumber = nextVersion,
            Name = request.Name.Trim(),
            BusinessPrompt = request.BusinessPrompt.Trim(),
            CreatedByUserID = GetAdministratorId(),
            CreatedAt = DateTime.UtcNow
        };
        _context.assistantpolicyversions.Add(version);
        AddAudit("assistant.policy.draft.create", "assistant_policy", nextVersion.ToString(), $"创建助手业务规则 v{nextVersion}");
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { version.PolicyVersionID, version.VersionNumber }, "草稿已创建", HttpContext.TraceIdentifier));
    }

    [HttpPost("policy/{policyVersionId:long}/publish")]
    public async Task<IActionResult> Publish(long policyVersionId, CancellationToken cancellationToken)
    {
        var target = await _context.assistantpolicyversions.FirstOrDefaultAsync(
            item => item.PolicyVersionID == policyVersionId, cancellationToken)
            ?? throw AppException.NotFound("业务规则版本不存在。");
        await _context.assistantpolicyversions.Where(item => item.IsPublished)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsPublished, false), cancellationToken);
        target.IsPublished = true;
        target.PublishedByUserID = GetAdministratorId();
        target.PublishedAt = DateTime.UtcNow;
        AddAudit("assistant.policy.publish", "assistant_policy", target.VersionNumber.ToString(), $"发布助手业务规则 v{target.VersionNumber}");
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { target.PolicyVersionID, target.VersionNumber }, "业务规则已发布", HttpContext.TraceIdentifier));
    }

    [HttpPut("roles/{role}")]
    public async Task<IActionResult> UpdateRole(
        string role,
        [FromBody] AdminUpdateAiRolePolicyRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<UserRole>(role, true, out var parsedRole) || !Enum.IsDefined(parsedRole))
            throw AppException.Validation("不支持的用户角色。");
        var allowed = NormalizeWorkflows(request.AllowedWorkflowCodes);
        var policy = await _context.airolepolicies.FirstOrDefaultAsync(item => item.Role == parsedRole, cancellationToken);
        if (policy == null)
        {
            policy = new AiRolePolicy { Role = parsedRole, CreatedAt = DateTime.UtcNow };
            _context.airolepolicies.Add(policy);
        }
        policy.AssistantEnabled = request.AssistantEnabled;
        policy.CanProposeGeneration = request.CanProposeGeneration;
        policy.CanExecuteGeneration = request.CanExecuteGeneration;
        policy.CanAutoAddToProject = request.CanAutoAddToProject;
        policy.MaxConcurrentJobs = request.MaxConcurrentJobs;
        policy.AllowedWorkflowCodesJson = JsonSerializer.Serialize(allowed);
        policy.UpdatedAt = DateTime.UtcNow;
        policy.UpdatedByUserID = GetAdministratorId();
        AddAudit("assistant.role_policy.update", "ai_role_policy", parsedRole.ToString(), $"修改 {parsedRole} 的 AI 权限");
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Role = parsedRole.ToString() }, "角色 AI 权限已更新", HttpContext.TraceIdentifier));
    }

    [HttpPut("users/{userId:int}")]
    public async Task<IActionResult> UpdateUserOverride(
        int userId,
        [FromBody] AdminUpdateAiUserOverrideRequest request,
        CancellationToken cancellationToken)
    {
        if (!await _context.users.AsNoTracking().AnyAsync(item => item.UserID == userId, cancellationToken))
            throw AppException.NotFound("用户不存在。");
        if (request.ExpiresAt <= DateTime.UtcNow)
            throw AppException.Validation("权限覆盖的过期时间必须晚于当前时间。");
        var item = await _context.aiuserpolicyoverrides.FirstOrDefaultAsync(value => value.UserID == userId, cancellationToken);
        if (item == null)
        {
            item = new AiUserPolicyOverride { UserID = userId, CreatedAt = DateTime.UtcNow };
            _context.aiuserpolicyoverrides.Add(item);
        }
        item.AssistantEnabled = request.AssistantEnabled;
        item.CanProposeGeneration = request.CanProposeGeneration;
        item.CanExecuteGeneration = request.CanExecuteGeneration;
        item.CanAutoAddToProject = request.CanAutoAddToProject;
        item.MaxConcurrentJobs = request.MaxConcurrentJobs;
        item.AllowedWorkflowCodesJson = request.AllowedWorkflowCodes == null
            ? null
            : JsonSerializer.Serialize(NormalizeWorkflows(request.AllowedWorkflowCodes));
        item.ExpiresAt = request.ExpiresAt;
        item.UpdatedAt = DateTime.UtcNow;
        item.UpdatedByUserID = GetAdministratorId();
        AddAudit("assistant.user_policy.update", "ai_user_policy", userId.ToString(), "修改用户 AI 权限覆盖");
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { UserId = userId }, "用户 AI 权限覆盖已保存", HttpContext.TraceIdentifier));
    }

    [HttpDelete("users/{userId:int}")]
    public async Task<IActionResult> DeleteUserOverride(int userId, CancellationToken cancellationToken)
    {
        var item = await _context.aiuserpolicyoverrides.FirstOrDefaultAsync(value => value.UserID == userId, cancellationToken);
        if (item != null) _context.aiuserpolicyoverrides.Remove(item);
        AddAudit("assistant.user_policy.delete", "ai_user_policy", userId.ToString(), "删除用户 AI 权限覆盖");
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("provider/test")]
    public async Task<IActionResult> TestProvider(CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        var result = await _modelClient.CompleteAsync(
            new AssistantBriefDto(),
            [new AssistantModelMessage("user", "请用一句话确认室内设计助手已连接，不要提出生图建议。")],
            await _governanceService.GetPublishedBusinessPromptAsync(cancellationToken),
            cancellationToken);
        timer.Stop();
        AddAudit("assistant.provider.test", "assistant_provider", result.Model, "测试助手模型连接");
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<object>.Ok(new
        {
            result.Model,
            LatencyMs = timer.ElapsedMilliseconds,
            result.InputTokens,
            result.OutputTokens
        }, "模型连接正常", HttpContext.TraceIdentifier));
    }

    [HttpPost("model-profiles/{profileId}/test")]
    public async Task<IActionResult> TestModelProfile(
        string profileId,
        CancellationToken cancellationToken)
    {
        var result = await _agentModelRouter.CompleteAsync(
            profileId,
            new AgentModelRequest(
                "你是模型连通性检测程序。不得调用工具，不得输出敏感配置。",
                [AgentModelInputMessage.Text("user", "请只回复：连接正常")],
                ResponseFormat: AgentModelResponseFormat.Text,
                ThinkingMode: AgentThinkingMode.Disabled,
                MaxOutputTokens: 64),
            cancellationToken);
        AddAudit("assistant.model_profile.test", "agent_model_profile", profileId, $"测试 Agent 模型配置 {profileId}");
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<object>.Ok(new
        {
            result.ProfileId,
            result.Provider,
            result.Model,
            result.DurationMs,
            result.InputTokens,
            result.OutputTokens,
            result.ProviderRequestId
        }, "模型配置连接正常", HttpContext.TraceIdentifier));
    }

    [HttpPost("agent-config/reload")]
    public async Task<IActionResult> ReloadAgentConfiguration(CancellationToken cancellationToken)
    {
        var snapshot = _agentConfigurationCatalog.Reload();
        AddAudit(
            "assistant.agent_config.reload",
            "agent_configuration",
            snapshot.Version,
            snapshot.Valid ? "重新加载 Agent 配置" : "重新加载 Agent 配置但校验未通过");
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<object>.Ok(new
        {
            snapshot.LoadedAt,
            snapshot.Version,
            snapshot.Valid,
            AgentCount = snapshot.Agents.Count,
            ToolCount = snapshot.ToolIds.Count,
            snapshot.ValidationErrors
        }, snapshot.Valid ? "Agent 配置已重新加载" : "Agent 配置已加载，但存在校验错误", HttpContext.TraceIdentifier));
    }

    private List<string> NormalizeWorkflows(IEnumerable<string> values)
    {
        var known = _workflowRegistry.GetAll().Select(item => item.WorkflowCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = values.Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var unknown = result.FirstOrDefault(item => !known.Contains(item));
        if (unknown != null) throw AppException.Validation($"工作流不存在：{unknown}");
        return result;
    }

    private int GetAdministratorId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : 0;

    private static string GetSafeBaseUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return string.Empty;
        var builder = new UriBuilder(uri)
        {
            UserName = string.Empty,
            Password = string.Empty,
            Query = string.Empty,
            Fragment = string.Empty
        };
        return builder.Uri.AbsoluteUri.TrimEnd('/');
    }

    private void AddAudit(string action, string targetType, string? targetId, string summary)
    {
        _context.adminauditlogs.Add(new AdminAuditLog
        {
            AdministratorUserID = GetAdministratorId(),
            Action = action,
            TargetType = targetType,
            TargetID = targetId,
            Summary = summary,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()[..Math.Min(Request.Headers.UserAgent.ToString().Length, 300)],
            RequestID = HttpContext.TraceIdentifier,
            CreatedAt = DateTime.UtcNow
        });
    }
}
