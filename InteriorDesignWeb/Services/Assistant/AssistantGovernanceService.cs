using System.Text.Json;
using InteriorDesignWeb.Application.Common;
using InteriorDesignWeb.Data;
using InteriorDesignWeb.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace InteriorDesignWeb.Services.Assistant;

public sealed record EffectiveAssistantPolicy(
    bool AssistantEnabled,
    bool CanProposeGeneration,
    bool CanExecuteGeneration,
    bool CanAutoAddToProject,
    int MaxConcurrentJobs,
    IReadOnlySet<string> AllowedWorkflowCodes);

public interface IAssistantGovernanceService
{
    Task<EffectiveAssistantPolicy> GetEffectivePolicyAsync(int userId, CancellationToken cancellationToken = default);
    Task<string> GetPublishedBusinessPromptAsync(CancellationToken cancellationToken = default);
}

public sealed class AssistantGovernanceService : IAssistantGovernanceService
{
    public const string CorePolicyVersion = "2026-07-14.1";
    public const string DefaultBusinessPrompt =
        "以专业室内设计总监的方式工作。用户缺少构思时主动给出明确方向，不把对话变成问卷。每个设计方向最多合并追问一次，只问最影响画面的信息；用户回答后用合理假设补齐细节，给出一个推荐方案并立即提出效果图生成建议。回复短、清楚、有排版，不复述完整提示词。";

    private readonly DesignHubContext _context;

    public AssistantGovernanceService(DesignHubContext context)
    {
        _context = context;
    }

    public async Task<EffectiveAssistantPolicy> GetEffectivePolicyAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.users.AsNoTracking()
            .Where(item => item.UserID == userId && item.IsEnabled)
            .Select(item => new { item.Role })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw AppException.Forbidden("账号不存在、已停用或无权使用 AI 助手。");

        var rolePolicy = await _context.airolepolicies.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Role == user.Role, cancellationToken);
        var defaults = Defaults(user.Role);
        var now = DateTime.UtcNow;
        var userOverride = await _context.aiuserpolicyoverrides.AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserID == userId
                && (item.ExpiresAt == null || item.ExpiresAt > now), cancellationToken);

        var roleWorkflows = ParseWorkflowCodes(rolePolicy?.AllowedWorkflowCodesJson);
        var effectiveWorkflows = userOverride?.AllowedWorkflowCodesJson == null
            ? roleWorkflows
            : ParseWorkflowCodes(userOverride.AllowedWorkflowCodesJson);

        return new EffectiveAssistantPolicy(
            userOverride?.AssistantEnabled ?? rolePolicy?.AssistantEnabled ?? defaults.AssistantEnabled,
            userOverride?.CanProposeGeneration ?? rolePolicy?.CanProposeGeneration ?? defaults.CanProposeGeneration,
            userOverride?.CanExecuteGeneration ?? rolePolicy?.CanExecuteGeneration ?? defaults.CanExecuteGeneration,
            userOverride?.CanAutoAddToProject ?? rolePolicy?.CanAutoAddToProject ?? defaults.CanAutoAddToProject,
            Math.Clamp(userOverride?.MaxConcurrentJobs ?? rolePolicy?.MaxConcurrentJobs ?? defaults.MaxConcurrentJobs, 1, 100),
            effectiveWorkflows);
    }

    public async Task<string> GetPublishedBusinessPromptAsync(CancellationToken cancellationToken = default) =>
        await _context.assistantpolicyversions.AsNoTracking()
            .Where(item => item.IsPublished)
            .OrderByDescending(item => item.PublishedAt)
            .ThenByDescending(item => item.VersionNumber)
            .Select(item => item.BusinessPrompt)
            .FirstOrDefaultAsync(cancellationToken)
        ?? DefaultBusinessPrompt;

    public static HashSet<string> ParseWorkflowCodes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new(StringComparer.OrdinalIgnoreCase);
        try
        {
            return (JsonSerializer.Deserialize<List<string>>(json) ?? [])
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static EffectiveAssistantPolicy Defaults(UserRole role) => new(
        true,
        true,
        true,
        true,
        role switch
        {
            UserRole.Member => 2,
            UserRole.PremiumMember => 3,
            UserRole.Administrator => 10,
            _ => 1
        },
        new HashSet<string>(StringComparer.OrdinalIgnoreCase));
}
