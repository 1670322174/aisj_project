using System.Net.Http.Json;
using System.Text.Json;
using InteriorDesignWeb.Config;

namespace InteriorDesignWeb.Services.Assistant.Models;

public sealed class MiniMaxAnthropicAgentModelClient : IAgentModelProviderClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MiniMaxAnthropicAgentModelClient> _logger;

    public MiniMaxAnthropicAgentModelClient(
        IHttpClientFactory httpClientFactory,
        ILogger<MiniMaxAnthropicAgentModelClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string ProviderId => AgentModelProviderIds.MiniMax;

    public async Task<AgentModelResponse> CompleteAsync(
        string profileId,
        AgentModelProfileOptions profile,
        AgentModelRequest request,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = profile.Model,
            ["max_tokens"] = Math.Clamp(request.MaxOutputTokens ?? profile.MaxOutputTokens, 64, 128000),
            ["system"] = request.SystemPrompt,
            ["messages"] = request.Messages.Select(BuildMessage).ToArray()
        };

        if (request.Temperature.HasValue)
        {
            payload["temperature"] = Math.Clamp(request.Temperature.Value, 0, 2);
        }

        var thinkingMode = request.ThinkingMode ?? ParseThinkingMode(profile.ThinkingMode);
        payload["thinking"] = new
        {
            type = thinkingMode == AgentThinkingMode.Disabled ? "disabled" : "adaptive"
        };

        if (request.Tools is { Count: > 0 })
        {
            payload["tools"] = request.Tools.Select(tool => new
            {
                name = tool.Name,
                description = tool.Description,
                input_schema = tool.Parameters
            }).ToArray();
            if (!string.IsNullOrWhiteSpace(request.ForcedToolName))
            {
                payload["tool_choice"] = new { type = "tool", name = request.ForcedToolName };
            }
        }

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            AgentModelHttpSupport.BuildEndpoint(profile.BaseUrl, "v1/messages"))
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        httpRequest.Headers.TryAddWithoutValidation("x-api-key", profile.ApiKey);
        httpRequest.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");

        var client = _httpClientFactory.CreateClient(nameof(MiniMaxAnthropicAgentModelClient));
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(profile.TimeoutSeconds, 15, 600));
        var httpResult = await AgentModelHttpSupport.SendAsync(
            client,
            httpRequest,
            ProviderId,
            profile.Model,
            _logger,
            cancellationToken);

        using var document = AgentModelHttpSupport.ParseResponse(
            httpResult,
            ProviderId,
            profile.Model,
            _logger);
        var root = document.RootElement;
        if (!root.TryGetProperty("content", out var contentBlocks)
            || contentBlocks.ValueKind != JsonValueKind.Array)
        {
            throw AgentModelHttpSupport.InvalidOutput(
                "provider_content_blocks_missing",
                "provider_response_shape",
                "MiniMax Anthropic 响应缺少 content 数组，请检查协议地址、模型名称和 Anthropic 兼容格式。",
                httpResult.RequestId);
        }

        var textParts = new List<string>();
        var toolCalls = new List<AgentModelToolCall>();
        foreach (var block in contentBlocks.EnumerateArray())
        {
            var type = block.TryGetProperty("type", out var typeElement)
                ? typeElement.GetString()
                : null;
            if (string.Equals(type, "text", StringComparison.OrdinalIgnoreCase)
                && block.TryGetProperty("text", out var textElement)
                && textElement.ValueKind == JsonValueKind.String)
            {
                var text = textElement.GetString();
                if (!string.IsNullOrWhiteSpace(text)) textParts.Add(text);
            }
            else if (string.Equals(type, "tool_use", StringComparison.OrdinalIgnoreCase)
                     && block.TryGetProperty("name", out var nameElement))
            {
                var name = nameElement.GetString();
                if (string.IsNullOrWhiteSpace(name)) continue;
                var id = block.TryGetProperty("id", out var idElement)
                    ? idElement.GetString() ?? Guid.NewGuid().ToString("N")
                    : Guid.NewGuid().ToString("N");
                var input = block.TryGetProperty("input", out var inputElement)
                    ? inputElement.Clone()
                    : EmptyObject();
                toolCalls.Add(new AgentModelToolCall(id, name, input));
            }
        }

        if (textParts.Count == 0 && toolCalls.Count == 0)
        {
            throw AgentModelHttpSupport.InvalidOutput(
                "provider_empty_model_output",
                "provider_response_shape",
                "MiniMax 响应中没有文本或工具调用，请结合上游请求 ID 排查。",
                httpResult.RequestId);
        }

        var usage = root.TryGetProperty("usage", out var usageElement)
            ? usageElement
            : default;
        var responseModel = root.TryGetProperty("model", out var modelElement)
            ? modelElement.GetString() ?? profile.Model
            : profile.Model;
        var responseId = root.TryGetProperty("id", out var responseIdElement)
            ? responseIdElement.GetString()
            : null;

        return new AgentModelResponse(
            profileId,
            ProviderId,
            responseModel,
            string.Join("\n", textParts).Trim(),
            toolCalls,
            AgentModelHttpSupport.ReadInt(usage, "input_tokens"),
            AgentModelHttpSupport.ReadInt(usage, "output_tokens"),
            httpResult.DurationMs,
            httpResult.RequestId ?? responseId,
            responseId,
            contentBlocks.GetRawText());
    }

    private static object BuildMessage(AgentModelInputMessage message) => new
    {
        role = message.Role,
        content = message.Content.Select(BuildContentPart).ToArray()
    };

    private static object BuildContentPart(AgentModelContentPart part)
    {
        if (string.Equals(part.Type, "text", StringComparison.OrdinalIgnoreCase))
        {
            return new { type = "text", text = part.Text ?? string.Empty };
        }
        if (string.Equals(part.Type, "image_url", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(part.ImageUrl))
        {
            return new
            {
                type = "image",
                source = new { type = "url", url = part.ImageUrl }
            };
        }
        throw AgentModelHttpSupport.ConfigurationUnavailable("MiniMax 请求包含不支持的内容类型。");
    }

    private static AgentThinkingMode ParseThinkingMode(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "enabled" => AgentThinkingMode.Enabled,
            "adaptive" => AgentThinkingMode.Adaptive,
            _ => AgentThinkingMode.Disabled
        };

    private static JsonElement EmptyObject()
    {
        using var document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
    }
}
