using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InteriorDesignWeb.Application.Common;
using InteriorDesignWeb.Config;
using InteriorDesignWeb.Models.DTOs.Assistant;
using Microsoft.Extensions.Options;

namespace InteriorDesignWeb.Services.Assistant;

public sealed class OpenAICompatibleAssistantModelClient : IAssistantModelClient
{
    private const string SystemPrompt = """
你是室内设计网站中的固定规则设计助手，不是自主智能体。你的职责是通过简洁对话补全空间、面积、风格、颜色、材质、功能、照明和限制条件，并在信息足够时提出生图草案。
只输出一个 JSON 对象，不要输出 Markdown 或代码围栏。字段必须为：assistantText、action、missingFields、brief、generationDraft。
action 只能是 ask_clarification、update_brief、propose_generation、explain_result、unsupported。
brief 必须包含 roomType、area、style、colors、materials、requirements、lighting、constraints、missingFields。
信息不足时逐轮询问最重要的 1 到 4 项，不要一次提出过多问题。信息足够时 action 使用 propose_generation，generationDraft 包含 generationType=text_to_image、prompt、negativePrompt、parameters。
不要输出真实 workflowCode、项目 ID、房间 ID、URL、密钥或工具调用。不要声称已经生成图片。提示词应面向专业室内设计效果图，明确空间、构图、材质、灯光、色彩和摄影表现。
""";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly AssistantOptions _options;
    private readonly ILogger<OpenAICompatibleAssistantModelClient> _logger;

    public OpenAICompatibleAssistantModelClient(
        HttpClient httpClient,
        IOptions<AssistantOptions> options,
        ILogger<OpenAICompatibleAssistantModelClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AssistantModelResult> CompleteAsync(
        AssistantBriefDto currentBrief,
        IReadOnlyList<AssistantModelMessage> messages,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled
            || string.IsNullOrWhiteSpace(_options.BaseUrl)
            || string.IsNullOrWhiteSpace(_options.ApiKey)
            || string.IsNullOrWhiteSpace(_options.Model)
            || !Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out _)
            || _httpClient.BaseAddress == null)
        {
            throw new AppException(
                ErrorCodes.AssistantUnavailable,
                "AI 设计助手尚未配置，请联系管理员。",
                StatusCodes.Status503ServiceUnavailable);
        }

        var requestMessages = new List<object>
        {
            new { role = "system", content = SystemPrompt },
            new
            {
                role = "system",
                content = "当前设计方案摘要：" + JsonSerializer.Serialize(currentBrief, JsonOptions)
            }
        };
        requestMessages.AddRange(messages.Select(message => (object)new
        {
            role = message.Role,
            content = message.Content
        }));

        var payload = new Dictionary<string, object?>
        {
            ["model"] = _options.Model,
            ["messages"] = requestMessages,
            ["temperature"] = 0.35,
            ["max_tokens"] = Math.Clamp(_options.MaxOutputTokens, 256, 8000)
        };
        if (_options.UseJsonResponseFormat)
        {
            payload["response_format"] = new { type = "json_object" };
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        var timer = Stopwatch.StartNew();
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("助手模型请求失败. StatusCode={StatusCode}", (int)response.StatusCode);
            throw new AppException(
                ErrorCodes.AssistantUnavailable,
                "AI 设计助手暂时不可用，请稍后重试。",
                StatusCodes.Status502BadGateway);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        timer.Stop();

        var root = document.RootElement;
        string? content;
        try
        {
            content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            _logger.LogWarning(ex, "助手模型响应缺少 choices.message.content");
            throw InvalidOutput();
        }
        if (string.IsNullOrWhiteSpace(content))
        {
            throw InvalidOutput();
        }

        var json = ExtractJson(content);
        AssistantModelOutputDto? output;
        try
        {
            output = JsonSerializer.Deserialize<AssistantModelOutputDto>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "助手模型返回了无法解析的结构化数据");
            throw InvalidOutput();
        }

        ValidateOutput(output);
        var usage = root.TryGetProperty("usage", out var usageElement) ? usageElement : default;
        var inputTokens = TryReadInt(usage, "prompt_tokens");
        var outputTokens = TryReadInt(usage, "completion_tokens");
        var model = root.TryGetProperty("model", out var modelElement)
            ? modelElement.GetString() ?? _options.Model
            : _options.Model;

        return new AssistantModelResult(output!, model, inputTokens, outputTokens, checked((int)Math.Min(timer.ElapsedMilliseconds, int.MaxValue)));
    }

    private static void ValidateOutput(AssistantModelOutputDto? output)
    {
        string[] actions = ["ask_clarification", "update_brief", "propose_generation", "explain_result", "unsupported"];
        if (output == null
            || string.IsNullOrWhiteSpace(output.AssistantText)
            || !actions.Contains(output.Action, StringComparer.OrdinalIgnoreCase)
            || output.Brief == null)
        {
            throw InvalidOutput();
        }

        output.AssistantText = output.AssistantText.Trim();
        output.Action = output.Action.Trim().ToLowerInvariant();
        output.MissingFields ??= new();
        output.Brief.Colors ??= new();
        output.Brief.Materials ??= new();
        output.Brief.Requirements ??= new();
        output.Brief.Constraints ??= new();
        output.Brief.MissingFields = output.MissingFields.Count > 0
            ? output.MissingFields
            : output.Brief.MissingFields ?? new();

        if (output.Action == "propose_generation")
        {
            if (output.GenerationDraft == null || string.IsNullOrWhiteSpace(output.GenerationDraft.Prompt))
            {
                throw InvalidOutput();
            }
            output.GenerationDraft.GenerationType = "text_to_image";
            output.GenerationDraft.Prompt = output.GenerationDraft.Prompt.Trim();
            output.GenerationDraft.NegativePrompt ??= string.Empty;
            output.GenerationDraft.Parameters ??= new();
            if (output.GenerationDraft.Prompt.Length > 8000) output.GenerationDraft.Prompt = output.GenerationDraft.Prompt[..8000];
            if (output.GenerationDraft.NegativePrompt.Length > 4000) output.GenerationDraft.NegativePrompt = output.GenerationDraft.NegativePrompt[..4000];
        }
    }

    private static string ExtractJson(string content)
    {
        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        return start >= 0 && end > start ? content[start..(end + 1)] : content;
    }

    private static int TryReadInt(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object
        && parent.TryGetProperty(name, out var value)
        && value.TryGetInt32(out var result)
            ? result
            : 0;

    private static AppException InvalidOutput() => new(
        ErrorCodes.AssistantOutputInvalid,
        "AI 助手返回的数据格式不完整，请重新发送消息。",
        StatusCodes.Status502BadGateway);
}
