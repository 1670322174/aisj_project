using System.Text.Json;
using InteriorDesignWeb.Config;
using Microsoft.Extensions.Options;

namespace InteriorDesignWeb.Services.Assistant.Models;

public sealed class AgentPlatformManifest
{
    public string Version { get; set; } = string.Empty;
    public string DefaultAgentId { get; set; } = string.Empty;
    public int MaxHandoffDepth { get; set; } = 2;
    public int MaxAgentsPerRun { get; set; } = 4;
    public List<string> AgentFiles { get; set; } = [];
}

public sealed class AgentDefinition
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string Mode { get; set; } = "production";
    public string DefaultModelProfile { get; set; } = string.Empty;
    public List<string> AllowedModelProfiles { get; set; } = [];
    public string SystemPromptFile { get; set; } = string.Empty;
    public List<string> SkillIds { get; set; } = [];
    public List<string> AllowedTools { get; set; } = [];
    public List<string> HandoffTargets { get; set; } = [];
    public List<string> PresentationModes { get; set; } = [];
    public int MaxSteps { get; set; } = 8;
    public int MaxOutputTokens { get; set; } = 4000;
    public int TimeoutSeconds { get; set; } = 120;
}

public sealed record AgentConfigurationSnapshot(
    DateTime LoadedAt,
    string RootPath,
    string Version,
    string DefaultAgentId,
    int MaxHandoffDepth,
    int MaxAgentsPerRun,
    IReadOnlyList<AgentDefinition> Agents,
    IReadOnlyList<string> ToolIds,
    IReadOnlyList<string> ValidationErrors)
{
    public bool Valid => ValidationErrors.Count == 0;
}

public interface IAgentConfigurationCatalog
{
    AgentConfigurationSnapshot Current { get; }
    AgentConfigurationSnapshot Reload();
}

public sealed class AgentConfigurationCatalog : IAgentConfigurationCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly IHostEnvironment _environment;
    private readonly IOptionsMonitor<AgentPlatformOptions> _options;
    private readonly IAgentModelRouter _modelRouter;
    private readonly ILogger<AgentConfigurationCatalog> _logger;
    private readonly object _sync = new();
    private AgentConfigurationSnapshot? _current;

    public AgentConfigurationCatalog(
        IHostEnvironment environment,
        IOptionsMonitor<AgentPlatformOptions> options,
        IAgentModelRouter modelRouter,
        ILogger<AgentConfigurationCatalog> logger)
    {
        _environment = environment;
        _options = options;
        _modelRouter = modelRouter;
        _logger = logger;
    }

    public AgentConfigurationSnapshot Current => _current ?? Reload();

    public AgentConfigurationSnapshot Reload()
    {
        lock (_sync)
        {
            _current = LoadCore();
            _logger.LogInformation(
                "Agent 配置已加载. Version={Version}, AgentCount={AgentCount}, ToolCount={ToolCount}, Valid={Valid}, ErrorCount={ErrorCount}",
                _current.Version,
                _current.Agents.Count,
                _current.ToolIds.Count,
                _current.Valid,
                _current.ValidationErrors.Count);
            return _current;
        }
    }

    private AgentConfigurationSnapshot LoadCore()
    {
        var errors = new List<string>();
        var root = ResolveRoot(_options.CurrentValue.ConfigurationRoot);
        var manifestPath = ResolveInsideRoot(root, "platform.json", errors);
        var manifest = ReadJson<AgentPlatformManifest>(manifestPath, "平台清单", errors) ?? new AgentPlatformManifest();
        var tools = LoadToolIds(root, errors);
        var agents = new List<AgentDefinition>();

        foreach (var relativePath in manifest.AgentFiles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var path = ResolveInsideRoot(root, relativePath, errors);
            var definition = ReadJson<AgentDefinition>(path, $"Agent 配置 {relativePath}", errors);
            if (definition != null) agents.Add(definition);
        }

        ValidateManifest(manifest, agents, tools, root, errors);
        return new AgentConfigurationSnapshot(
            DateTime.UtcNow,
            root,
            manifest.Version,
            manifest.DefaultAgentId,
            manifest.MaxHandoffDepth,
            manifest.MaxAgentsPerRun,
            agents.OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase).ToList(),
            tools.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList(),
            errors.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    private void ValidateManifest(
        AgentPlatformManifest manifest,
        IReadOnlyList<AgentDefinition> agents,
        IReadOnlySet<string> toolIds,
        string root,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(manifest.Version)) errors.Add("platform.json 缺少 version。 ");
        if (manifest.MaxHandoffDepth is < 1 or > 5) errors.Add("maxHandoffDepth 必须为 1-5。 ");
        if (manifest.MaxAgentsPerRun is < 1 or > 20) errors.Add("maxAgentsPerRun 必须为 1-20。 ");

        var duplicateAgent = agents.GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1);
        if (duplicateAgent != null) errors.Add($"Agent id 为空或重复：{duplicateAgent.Key}");
        var agentIds = agents.Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .Select(item => item.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!agentIds.Contains(manifest.DefaultAgentId)) errors.Add("defaultAgentId 指向不存在的 Agent。 ");

        var modelProfiles = _modelRouter.GetProfileStatuses()
            .Select(item => item.ProfileId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var agent in agents)
        {
            ValidateAgent(agent, agentIds, modelProfiles, toolIds, root, errors);
        }
    }

    private static void ValidateAgent(
        AgentDefinition agent,
        IReadOnlySet<string> agentIds,
        IReadOnlySet<string> modelProfiles,
        IReadOnlySet<string> toolIds,
        string root,
        ICollection<string> errors)
    {
        var prefix = string.IsNullOrWhiteSpace(agent.Id) ? "未命名 Agent" : agent.Id;
        if (string.IsNullOrWhiteSpace(agent.DisplayName)) errors.Add($"{prefix}: displayName 未配置。 ");
        if (!new[] { "production", "test", "disabled" }.Contains(agent.Mode, StringComparer.OrdinalIgnoreCase))
            errors.Add($"{prefix}: mode 只允许 production、test 或 disabled。 ");
        if (!agent.AllowedModelProfiles.Contains(agent.DefaultModelProfile, StringComparer.OrdinalIgnoreCase))
            errors.Add($"{prefix}: defaultModelProfile 不在 allowedModelProfiles 中。 ");
        foreach (var profile in agent.AllowedModelProfiles)
        {
            if (!modelProfiles.Contains(profile)) errors.Add($"{prefix}: 模型配置不存在：{profile}");
        }
        if (agent.MaxSteps is < 1 or > 50) errors.Add($"{prefix}: maxSteps 必须为 1-50。 ");
        if (agent.MaxOutputTokens is < 64 or > 128000) errors.Add($"{prefix}: maxOutputTokens 必须为 64-128000。 ");
        if (agent.TimeoutSeconds is < 15 or > 600) errors.Add($"{prefix}: timeoutSeconds 必须为 15-600。 ");

        var promptPath = ResolveInsideRoot(root, agent.SystemPromptFile, errors);
        if (!File.Exists(promptPath)) errors.Add($"{prefix}: 系统提示词文件不存在：{agent.SystemPromptFile}");
        foreach (var skillId in agent.SkillIds)
        {
            var skillPath = ResolveInsideRoot(root, Path.Combine("skills", skillId, "SKILL.md"), errors);
            if (!File.Exists(skillPath)) errors.Add($"{prefix}: Skill 不存在：{skillId}");
        }
        foreach (var tool in agent.AllowedTools)
        {
            if (!toolIds.Contains(tool)) errors.Add($"{prefix}: 工具目录中不存在：{tool}");
        }
        foreach (var target in agent.HandoffTargets)
        {
            if (!agentIds.Contains(target)) errors.Add($"{prefix}: handoffTargets 指向不存在的 Agent：{target}");
        }
    }

    private HashSet<string> LoadToolIds(string root, ICollection<string> errors)
    {
        var path = ResolveInsideRoot(root, Path.Combine("tools", "catalog.json"), errors);
        var catalog = ReadJson<ToolCatalog>(path, "工具目录", errors);
        if (catalog == null) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var duplicate = catalog.Tools.GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1);
        if (duplicate != null) errors.Add($"工具 id 为空或重复：{duplicate.Key}");
        return catalog.Tools.Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .Select(item => item.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private string ResolveRoot(string configuredRoot)
    {
        var value = string.IsNullOrWhiteSpace(configuredRoot) ? "AIAgent" : configuredRoot.Trim();
        return Path.GetFullPath(Path.IsPathRooted(value)
            ? value
            : Path.Combine(_environment.ContentRootPath, value));
    }

    private static string ResolveInsideRoot(string root, string relativePath, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return root;
        var resolved = Path.GetFullPath(Path.Combine(root, relativePath));
        var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"配置路径越过 AIAgent 根目录：{relativePath}");
            return root;
        }
        return resolved;
    }

    private static T? ReadJson<T>(string path, string description, ICollection<string> errors)
    {
        if (!File.Exists(path))
        {
            errors.Add($"{description}文件不存在：{path}");
            return default;
        }
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            errors.Add($"{description}读取失败：{exception.Message}");
            return default;
        }
    }

    private sealed class ToolCatalog
    {
        public List<ToolEntry> Tools { get; set; } = [];
    }

    private sealed class ToolEntry
    {
        public string Id { get; set; } = string.Empty;
    }
}

public sealed class AgentConfigurationStartupValidator : IHostedService
{
    private readonly IAgentConfigurationCatalog _catalog;
    private readonly IOptions<AgentPlatformOptions> _options;

    public AgentConfigurationStartupValidator(
        IAgentConfigurationCatalog catalog,
        IOptions<AgentPlatformOptions> options)
    {
        _catalog = catalog;
        _options = options;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var snapshot = _catalog.Reload();
        if (_options.Value.Enabled && _options.Value.StrictValidation && !snapshot.Valid)
        {
            throw new InvalidOperationException(
                $"Agent 配置校验失败：{string.Join("；", snapshot.ValidationErrors)}");
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
