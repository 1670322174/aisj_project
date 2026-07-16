using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InteriorDesignWeb.Config;

namespace InteriorDesignWeb.Services.Assistant.Models;

public sealed class DeepSeekAgentModelClient : IAgentModelProviderClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DeepSeekAgentModelClient> _logger;

    public DeepSeekAgentModelClient(
        IHttpClientFactory httpClientFactory,
        ILogger<DeepSeekAgentModelClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string ProviderId => AgentModelProviderIds.DeepSeek;

    public async Task<AgentModelResponse> CompleteAsync(
        string profileId,
        AgentModelProfileOptions profile,
        AgentModelRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureTextOnly(request.Messages);
        var messages = new List<object>();
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            messages.Add(new { role = "system", content = request.SystemPrompt });
        }
        messages.AddRange(request.Messages.Select(message => (object)new
        {
            role = message.Role,
            content = string.Join("\n", message.Content.Select(item => item.Text ?? string.Empty))
        }));

        var thinkingMode = request.ThinkingMode ?? ParseThinkingMode(profile.ThinkingMode);
        var payload = new Dictionary<string, object?>
        {
            ["model"] = profile.Model,
            ["messages"] = messages,
            ["max_tokens"] = Math.Clamp(request.MaxOutputTokens ?? profile.MaxOutputTokens, 64, 128000),
            ["thinking"] = new
            {
                type = thinkingMode == AgentThinkingMode.Disabled ? "disabled" : "enabled"
            }
        };

        if (thinkingMode != AgentThinkingMode.Disabled)
        {
            payload["reasoning_effort"] = NormalizeReasoningEffort(
                request.ReasoningEffort ?? profile.ReasoningEffort);
        }

        if (request.Temperature.HasValue)
        {
            payload["temperature"] = Math.Clamp(request.Temperature.Value, 0, 2);
        }
        if (request.ResponseFormat == AgentModelResponseFormat.JsonObject)
        {
            payload["response_format"] = new { type = "json_object" };
        }
        if (request.Tools is { Count: > 0 })
        {
            payload["tools"] = request.Tools.Select(tool => new
            {
                type = "function",
                function = new
                {
                    name = tool.Name,
                    description = tool.Description,
                    parameters = tool.Parameters
                }
            }).ToArray();
            if (!string.IsNullOrWhiteSpace(request.ForcedToolName))
            {
                payload["tool_choice"] = new
                {
                    type = "function",
                    function = new { name = request.ForcedToolName }
                };
            }
        }

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            AgentModelHttpSupport.BuildEndpoint(profile.BaseUrl, "chat/completions"))
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", profile.ApiKey);

        var client = _httpClientFactory.CreateClient(nameof(DeepSeekAgentModelClient));
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
        if (!root.TryGetProperty("choices", out var choices)
            || choices.ValueKind != JsonValueKind.Array
            || choices.GetArrayLength() == 0
            || !choices[0].TryGetProperty("message", out var responseMessage))
        {
            throw AgentModelHttpSupport.InvalidOutput(
                "provider_choices_missing",
                "provider_response_shape",
                "DeepSeek 响应缺少 choices[0].message，请检查 OpenAI 兼容协议和模型配置。",
                httpResult.RequestId);
        }

        var content = responseMessage.TryGetProperty("content", out var contentElement)
                      && contentElement.ValueKind == JsonValueKind.String
            ? contentElement.GetString()?.Trim() ?? string.Empty
            : string.Empty;
        var toolCalls = ParseToolCalls(responseMessage);
        if (content.Length == 0 && toolCalls.Count == 0)
        {
            throw AgentModelHttpSupport.InvalidOutput(
                "provider_empty_model_output",
                "provider_response_shape",
                "DeepSeek 响应中没有文本或工具调用，请结合上游请求 ID 排查。",
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
            content,
            toolCalls,
            AgentModelHttpSupport.ReadInt(usage, "prompt_tokens"),
            AgentModelHttpSupport.ReadInt(usage, "completion_tokens"),
            httpResult.DurationMs,
            httpResult.RequestId ?? responseId,
            responseId,
            responseMessage.GetRawText());
    }

    private static List<AgentModelToolCall> ParseToolCalls(JsonElement message)
    {
        var result = new List<AgentModelToolCall>();
        if (!message.TryGetProperty("tool_calls", out var toolCalls)
            || toolCalls.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var toolCall in toolCalls.EnumerateArray())
        {
            if (!toolCall.TryGetProperty("function", out var function)
                || !function.TryGetProperty("name", out var nameElement))
            {
                continue;
            }
            var name = nameElement.GetString();
            if (string.IsNullOrWhiteSpace(name)) continue;
            var id = toolCall.TryGetProperty("id", out var idElement)
                ? idElement.GetString() ?? Guid.NewGuid().ToString("N")
                : Guid.NewGuid().ToString("N");
            var arguments = function.TryGetProperty("arguments", out var argumentsElement)
                ? ParseArguments(argumentsElement)
                : EmptyObject();
            result.Add(new AgentModelToolCall(id, name, arguments));
        }
        return result;
    }

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

    private static void EnsureTextOnly(IEnumerable<AgentModelInputMessage> messages)
    {
        if (messages.SelectMany(message => message.Content)
            .Any(part => !string.Equals(part.Type, "text", StringComparison.OrdinalIgnoreCase)))
        {
            throw AgentModelHttpSupport.ConfigurationUnavailable(
                "DeepSeek 文本模型不接受图片输入，请先调用视觉 Agent。");
        }
    }

    private static AgentThinkingMode ParseThinkingMode(string value) =>
        value.Trim().ToLowerInvariant() == "disabled"
            ? AgentThinkingMode.Disabled
            : AgentThinkingMode.Enabled;

    private static string NormalizeReasoningEffort(string value) =>
        value.Trim().Equals("max", StringComparison.OrdinalIgnoreCase) ? "max" : "high";

    private static JsonElement EmptyObject()
    {
        using var document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
    }
}
