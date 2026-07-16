using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InteriorDesignWeb.Config;

namespace InteriorDesignWeb.Services.Assistant.Models;

public sealed class VolcArkResponsesAgentModelClient : IAgentModelProviderClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<VolcArkResponsesAgentModelClient> _logger;

    public VolcArkResponsesAgentModelClient(
        IHttpClientFactory httpClientFactory,
        ILogger<VolcArkResponsesAgentModelClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string ProviderId => AgentModelProviderIds.VolcArk;

    public async Task<AgentModelResponse> CompleteAsync(
        string profileId,
        AgentModelProfileOptions profile,
        AgentModelRequest request,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = profile.Model,
            ["instructions"] = request.SystemPrompt,
            ["input"] = request.Messages.Select(BuildMessage).ToArray(),
            ["max_output_tokens"] = Math.Clamp(
                request.MaxOutputTokens ?? profile.MaxOutputTokens,
                64,
                128000),
            ["store"] = false
        };

        var thinkingMode = request.ThinkingMode ?? ParseThinkingMode(profile.ThinkingMode);
        payload["thinking"] = new
        {
            type = thinkingMode == AgentThinkingMode.Disabled ? "disabled" : "enabled"
        };

        if (request.Temperature.HasValue)
        {
            payload["temperature"] = Math.Clamp(request.Temperature.Value, 0, 2);
        }
        if (request.ResponseFormat == AgentModelResponseFormat.JsonObject)
        {
            payload["text"] = new { format = new { type = "json_object" } };
        }
        if (request.Tools is { Count: > 0 })
        {
            payload["tools"] = request.Tools.Select(tool => new
            {
                type = "function",
                name = tool.Name,
                description = tool.Description,
                parameters = tool.Parameters
            }).ToArray();
            if (!string.IsNullOrWhiteSpace(request.ForcedToolName))
            {
                payload["tool_choice"] = new { type = "function", name = request.ForcedToolName };
            }
        }

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            AgentModelHttpSupport.BuildEndpoint(profile.BaseUrl, "responses"))
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", profile.ApiKey);

        var client = _httpClientFactory.CreateClient(nameof(VolcArkResponsesAgentModelClient));
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
        if (!root.TryGetProperty("output", out var output)
            || output.ValueKind != JsonValueKind.Array)
        {
            throw AgentModelHttpSupport.InvalidOutput(
                "provider_output_array_missing",
                "provider_response_shape",
                "火山方舟 Responses 响应缺少 output 数组，请检查 Responses API 路径和模型配置。",
                httpResult.RequestId);
        }

        var textParts = new List<string>();
        var toolCalls = new List<AgentModelToolCall>();
        foreach (var item in output.EnumerateArray())
        {
            var type = item.TryGetProperty("type", out var typeElement)
                ? typeElement.GetString()
                : null;
            if (string.Equals(type, "message", StringComparison.OrdinalIgnoreCase)
                && item.TryGetProperty("content", out var content)
                && content.ValueKind == JsonValueKind.Array)
            {
                foreach (var part in content.EnumerateArray())
                {
                    if (part.TryGetProperty("type", out var partType)
                        && string.Equals(partType.GetString(), "output_text", StringComparison.OrdinalIgnoreCase)
                        && part.TryGetProperty("text", out var textElement)
                        && textElement.ValueKind == JsonValueKind.String)
                    {
                        var text = textElement.GetString();
                        if (!string.IsNullOrWhiteSpace(text)) textParts.Add(text);
                    }
                }
            }
            else if (string.Equals(type, "function_call", StringComparison.OrdinalIgnoreCase)
                     && item.TryGetProperty("name", out var nameElement))
            {
                var name = nameElement.GetString();
                if (string.IsNullOrWhiteSpace(name)) continue;
                var id = item.TryGetProperty("call_id", out var callIdElement)
                    ? callIdElement.GetString()
                    : null;
                id ??= item.TryGetProperty("id", out var idElement)
                    ? idElement.GetString()
                    : null;
                id ??= Guid.NewGuid().ToString("N");
                var arguments = item.TryGetProperty("arguments", out var argumentsElement)
                    ? ParseArguments(argumentsElement)
                    : EmptyObject();
                toolCalls.Add(new AgentModelToolCall(id, name, arguments));
            }
        }

        if (textParts.Count == 0 && toolCalls.Count == 0)
        {
            throw AgentModelHttpSupport.InvalidOutput(
                "provider_empty_model_output",
                "provider_response_shape",
                "火山方舟响应中没有文本或工具调用，请结合上游请求 ID 排查。",
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
            output.GetRawText());
    }

    private static object BuildMessage(AgentModelInputMessage message) => new
    {
        type = "message",
        role = message.Role,
        content = message.Content.Select(BuildContentPart).ToArray()
    };

    private static object BuildContentPart(AgentModelContentPart part)
    {
        if (string.Equals(part.Type, "text", StringComparison.OrdinalIgnoreCase))
        {
            return new { type = "input_text", text = part.Text ?? string.Empty };
        }
        if (string.Equals(part.Type, "image_url", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(part.ImageUrl))
        {
            return new
            {
                type = "input_image",
                image_url = part.ImageUrl,
                detail = NormalizeDetail(part.Detail)
            };
        }
        throw AgentModelHttpSupport.ConfigurationUnavailable("火山方舟请求包含不支持的内容类型。");
    }

    private static string NormalizeDetail(string? detail) =>
        detail?.Trim().ToLowerInvariant() switch
        {
            "low" => "low",
            "high" => "high",
            _ => "auto"
        };

    private static AgentThinkingMode ParseThinkingMode(string value) =>
        value.Trim().ToLowerInvariant() == "disabled"
            ? AgentThinkingMode.Disabled
            : AgentThinkingMode.Enabled;

    private static JsonElement ParseArguments(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object) return element.Clone();
        if (element.ValueKind != JsonValueKind.String) return EmptyObject();
        var value = element.GetString();
        if (string.IsNullOrWhiteSpace(value)) return EmptyObject();
        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            using var document = JsonDocument.Parse(JsonSerializer.Serialize(new { raw = value }));
            return document.RootElement.Clone();
        }
    }

    private static JsonElement EmptyObject()
    {
        using var document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
    }
}
