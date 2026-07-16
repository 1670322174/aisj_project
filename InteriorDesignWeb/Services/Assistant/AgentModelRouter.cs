using InteriorDesignWeb.Config;
using Microsoft.Extensions.Options;

namespace InteriorDesignWeb.Services.Assistant.Models;

public sealed class AgentModelRouter : IAgentModelRouter
{
    private readonly IOptionsMonitor<AgentModelsOptions> _options;
    private readonly IReadOnlyDictionary<string, IAgentModelProviderClient> _clients;
    private readonly ILogger<AgentModelRouter> _logger;

    public AgentModelRouter(
        IOptionsMonitor<AgentModelsOptions> options,
        IEnumerable<IAgentModelProviderClient> clients,
        ILogger<AgentModelRouter> logger)
    {
        _options = options;
        _clients = clients.ToDictionary(
            item => item.ProviderId,
            StringComparer.OrdinalIgnoreCase);
        _logger = logger;
    }

    public async Task<AgentModelResponse> CompleteAsync(
        string profileId,
        AgentModelRequest request,
        CancellationToken cancellationToken = default)
    {
        var (resolvedId, profile) = FindProfile(profileId);
        var status = BuildStatus(resolvedId, profile);
        if (!status.Enabled)
        {
            throw AgentModelHttpSupport.ConfigurationUnavailable("该 Agent 模型配置已停用。");
        }
        if (!status.Configured)
        {
            throw AgentModelHttpSupport.ConfigurationUnavailable(
                $"Agent 模型配置不完整：{string.Join("；", status.ValidationErrors)}");
        }
        if (request.Tools is { Count: > 0 } && !profile.Capabilities.Tools)
        {
            throw AgentModelHttpSupport.ConfigurationUnavailable("该模型配置不允许工具调用。");
        }
        var containsImages = request.Messages.SelectMany(item => item.Content)
            .Any(item => string.Equals(item.Type, "image_url", StringComparison.OrdinalIgnoreCase));
        if (containsImages && !profile.Capabilities.Vision)
        {
            throw AgentModelHttpSupport.ConfigurationUnavailable("该模型配置不支持图片输入。");
        }
        if (request.ResponseFormat == AgentModelResponseFormat.JsonObject
            && !profile.Capabilities.JsonOutput)
        {
            throw AgentModelHttpSupport.ConfigurationUnavailable("该模型配置不支持 JSON 输出模式。");
        }
        if (!_clients.TryGetValue(profile.Provider, out var client))
        {
            throw AgentModelHttpSupport.ConfigurationUnavailable("模型 Provider 尚未注册。");
        }

        _logger.LogInformation(
            "Agent 模型路由开始. ProfileId={ProfileId}, Provider={Provider}, Model={Model}, MessageCount={MessageCount}, ToolCount={ToolCount}, HasVisionInput={HasVisionInput}",
            resolvedId,
            profile.Provider,
            profile.Model,
            request.Messages.Count,
            request.Tools?.Count ?? 0,
            containsImages);
        var result = await client.CompleteAsync(
            resolvedId,
            profile,
            request,
            cancellationToken);
        _logger.LogInformation(
            "Agent 模型路由完成. ProfileId={ProfileId}, Provider={Provider}, Model={Model}, DurationMs={DurationMs}, InputTokens={InputTokens}, OutputTokens={OutputTokens}, ToolCallCount={ToolCallCount}, ProviderRequestId={ProviderRequestId}",
            result.ProfileId,
            result.Provider,
            result.Model,
            result.DurationMs,
            result.InputTokens,
            result.OutputTokens,
            result.ToolCalls.Count,
            result.ProviderRequestId);
        return result;
    }

    public IReadOnlyList<AgentModelProfileStatus> GetProfileStatuses() =>
        _options.CurrentValue.Profiles
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item => BuildStatus(item.Key, item.Value))
            .ToList();

    public AgentModelProfileStatus GetProfileStatus(string profileId)
    {
        var (resolvedId, profile) = FindProfile(profileId);
        return BuildStatus(resolvedId, profile);
    }

    private (string ProfileId, AgentModelProfileOptions Profile) FindProfile(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            throw AgentModelHttpSupport.ConfigurationUnavailable("未指定 Agent 模型配置。");
        }

        var match = _options.CurrentValue.Profiles.FirstOrDefault(
            item => item.Key.Equals(profileId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(match.Key) || match.Value == null)
        {
            throw AgentModelHttpSupport.ConfigurationUnavailable("Agent 模型配置不存在。");
        }
        return (match.Key, match.Value);
    }

    private AgentModelProfileStatus BuildStatus(
        string profileId,
        AgentModelProfileOptions profile)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(profile.Provider)) errors.Add("Provider 未配置");
        else if (!AgentModelProviderIds.All.Contains(profile.Provider)) errors.Add("Provider 不受支持");
        else if (!_clients.ContainsKey(profile.Provider)) errors.Add("Provider 客户端未注册");
        if (string.IsNullOrWhiteSpace(profile.Protocol)) errors.Add("Protocol 未配置");
        if (!Uri.TryCreate(profile.BaseUrl, UriKind.Absolute, out _)) errors.Add("BaseUrl 无效");
        if (string.IsNullOrWhiteSpace(profile.Model)) errors.Add("Model 未配置");
        if (string.IsNullOrWhiteSpace(profile.ApiKey)) errors.Add("ApiKey 未配置");
        if (profile.TimeoutSeconds is < 15 or > 600) errors.Add("TimeoutSeconds 必须为 15-600");
        if (profile.MaxOutputTokens is < 64 or > 128000) errors.Add("MaxOutputTokens 必须为 64-128000");
        ValidateProtocol(profile, errors);

        return new AgentModelProfileStatus(
            profileId,
            profile.Enabled,
            profile.Enabled && errors.Count == 0,
            profile.Provider,
            profile.Protocol,
            AgentModelHttpSupport.SafeBaseUrl(profile.BaseUrl),
            profile.Model,
            !string.IsNullOrWhiteSpace(profile.ApiKey),
            profile.TimeoutSeconds,
            profile.MaxOutputTokens,
            profile.ThinkingMode,
            profile.ReasoningEffort,
            profile.Capabilities,
            errors);
    }

    private static void ValidateProtocol(
        AgentModelProfileOptions profile,
        ICollection<string> errors)
    {
        var expected = profile.Provider.Trim().ToLowerInvariant() switch
        {
            AgentModelProviderIds.MiniMax => "anthropic",
            AgentModelProviderIds.DeepSeek => "openai",
            AgentModelProviderIds.VolcArk => "responses",
            _ => string.Empty
        };
        if (expected.Length > 0
            && !profile.Protocol.Equals(expected, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"{profile.Provider} 必须使用 {expected} 协议");
        }
    }
}
