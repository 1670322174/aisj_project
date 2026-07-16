using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using InteriorDesignWeb.Application.Common;

namespace InteriorDesignWeb.Services.Assistant.Models;

internal sealed record AgentModelHttpResult(
    string Body,
    string? RequestId,
    int DurationMs);

internal static class AgentModelHttpSupport
{
    public static Uri BuildEndpoint(string baseUrl, string relativePath)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            throw ConfigurationUnavailable(
                "模型服务地址无效。",
                "provider_base_url_invalid",
                "provider_configuration",
                "请管理员检查对应模型配置的 BaseUrl，必须是完整的 HTTP 或 HTTPS 地址。");
        }

        var normalized = baseUri.AbsoluteUri.EndsWith('/')
            ? baseUri
            : new Uri(baseUri.AbsoluteUri + "/");
        return new Uri(normalized, relativePath.TrimStart('/'));
    }

    public static async Task<AgentModelHttpResult> SendAsync(
        HttpClient client,
        HttpRequestMessage request,
        string provider,
        string model,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            timer.Stop();
            logger.LogWarning(
                ex,
                "Agent 模型请求超时. Provider={Provider}, Model={Model}, ProviderHost={ProviderHost}, DurationMs={DurationMs}",
                provider,
                model,
                request.RequestUri?.Host,
                timer.ElapsedMilliseconds);
            throw new AppException(
                ErrorCodes.AssistantUnavailable,
                "AI 模型响应超时，请稍后重试。",
                StatusCodes.Status504GatewayTimeout,
                ex).WithDiagnostic(
                    "provider_timeout",
                    "provider_http_request",
                    "检查服务器到模型服务的网络、供应商状态和 TimeoutSeconds；可稍后重试。",
                    retryable: true);
        }
        catch (HttpRequestException ex)
        {
            timer.Stop();
            logger.LogWarning(
                ex,
                "Agent 模型网络请求失败. Provider={Provider}, Model={Model}, ProviderHost={ProviderHost}, DurationMs={DurationMs}, HttpError={HttpError}",
                provider,
                model,
                request.RequestUri?.Host,
                timer.ElapsedMilliseconds,
                ex.HttpRequestError);
            throw new AppException(
                ErrorCodes.AssistantUnavailable,
                "无法连接 AI 模型服务，请稍后重试。",
                StatusCodes.Status502BadGateway,
                ex).WithDiagnostic(
                    "provider_network_error",
                    "provider_http_request",
                    "检查 BaseUrl、DNS、TLS 证书、服务器出站网络和防火墙。",
                    retryable: true);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            timer.Stop();
            var requestId = GetRequestId(response);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Agent 模型请求失败. Provider={Provider}, Model={Model}, StatusCode={StatusCode}, ProviderRequestId={ProviderRequestId}, DurationMs={DurationMs}, ProviderError={ProviderError}",
                    provider,
                    model,
                    (int)response.StatusCode,
                    requestId,
                    timer.ElapsedMilliseconds,
                    ExtractProviderError(body));
                var statusCode = (int)response.StatusCode;
                var (message, reason, hint, retryable) = DescribeProviderFailure(statusCode);
                throw new AppException(
                    ErrorCodes.AssistantUnavailable,
                    message,
                    StatusCodes.Status502BadGateway).WithDiagnostic(
                        reason,
                        "provider_http_response",
                        hint,
                        requestId,
                        retryable);
            }

            return new AgentModelHttpResult(
                body,
                requestId,
                checked((int)Math.Min(timer.ElapsedMilliseconds, int.MaxValue)));
        }
    }

    public static JsonDocument ParseResponse(
        AgentModelHttpResult response,
        string provider,
        string model,
        ILogger logger)
    {
        try
        {
            return JsonDocument.Parse(response.Body);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(
                ex,
                "Agent 模型返回的 HTTP 内容不是有效 JSON. Provider={Provider}, Model={Model}, ProviderRequestId={ProviderRequestId}, ResponseLength={ResponseLength}",
                provider,
                model,
                response.RequestId,
                response.Body.Length);
            throw InvalidOutput(
                "provider_invalid_json",
                "provider_response_parse",
                "供应商返回的外层内容不是 JSON；检查 BaseUrl、反向代理和协议类型是否匹配。",
                response.RequestId);
        }
    }

    public static int ReadInt(JsonElement parent, string propertyName) =>
        parent.ValueKind == JsonValueKind.Object
        && parent.TryGetProperty(propertyName, out var value)
        && value.TryGetInt32(out var result)
            ? result
            : 0;

    public static string SafeBaseUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return string.Empty;
        return new UriBuilder(uri)
        {
            UserName = string.Empty,
            Password = string.Empty,
            Query = string.Empty,
            Fragment = string.Empty
        }.Uri.AbsoluteUri.TrimEnd('/');
    }

    public static AppException ConfigurationUnavailable(
        string message,
        string reason = "agent_configuration_unavailable",
        string stage = "agent_configuration",
        string hint = "请管理员检查 AgentPlatform、Agent 配置目录和模型配置状态。") => new AppException(
            ErrorCodes.AssistantUnavailable,
            message,
            StatusCodes.Status503ServiceUnavailable)
        .WithDiagnostic(reason, stage, hint, retryable: false);

    public static AppException InvalidOutput(
        string reason = "provider_output_invalid",
        string stage = "provider_output_validation",
        string hint = "检查模型是否支持工具调用及当前 Agent 的结构化输出规则；必要时查看对应 Run 事件。",
        string? upstreamRequestId = null) => new AppException(
            ErrorCodes.AssistantOutputInvalid,
            "AI 模型返回的数据格式不完整，请重试。",
            StatusCodes.Status502BadGateway)
        .WithDiagnostic(reason, stage, hint, upstreamRequestId, retryable: true);

    private static (string Message, string Reason, string Hint, bool Retryable) DescribeProviderFailure(int statusCode) =>
        statusCode switch
        {
            400 => ("AI 模型未接受本次请求格式。", "provider_bad_request", "检查模型协议、模型名称、工具 schema 和输出参数。", false),
            401 or 403 => ("AI 模型鉴权失败，请联系管理员检查模型密钥和权限。", "provider_auth_failed", "检查 ApiKey、模型访问权限、账户状态和供应商区域。", false),
            404 => ("AI 模型接口或模型名称不存在。", "provider_not_found", "检查 BaseUrl、协议路径和 Model 配置。", false),
            408 => ("AI 模型服务等待请求超时。", "provider_request_timeout", "检查供应商状态并稍后重试。", true),
            409 => ("AI 模型服务拒绝了当前请求状态。", "provider_conflict", "结合供应商请求 ID 检查重复请求或会话状态。", true),
            429 => ("AI 模型请求过于频繁，请稍后重试。", "provider_rate_limited", "检查供应商额度、并发限制和限流策略。", true),
            >= 500 => ("AI 模型供应商暂时不可用，请稍后重试。", "provider_server_error", "结合供应商请求 ID 检查服务状态，稍后重试。", true),
            _ => ("AI 模型服务拒绝了本次请求。", $"provider_http_{statusCode}", "结合供应商请求 ID 和服务器日志排查。", false)
        };

    private static string? GetRequestId(HttpResponseMessage response)
    {
        foreach (var name in new[] { "x-request-id", "request-id", "trace-id", "x-trace-id" })
        {
            if (response.Headers.TryGetValues(name, out var values))
            {
                return values.FirstOrDefault();
            }
        }
        return null;
    }

    private static string ExtractProviderError(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return "empty_response";
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            string? message = null;
            if (root.TryGetProperty("error", out var error))
            {
                message = error.ValueKind == JsonValueKind.String
                    ? error.GetString()
                    : error.ValueKind == JsonValueKind.Object
                      && error.TryGetProperty("message", out var errorMessage)
                        ? errorMessage.GetString()
                        : null;
            }
            if (message == null
                && root.TryGetProperty("message", out var directMessage)
                && directMessage.ValueKind == JsonValueKind.String)
            {
                message = directMessage.GetString();
            }

            if (string.IsNullOrWhiteSpace(message)) return "structured_error_without_message";
            var safe = RedactSensitive(message.Replace('\r', ' ').Replace('\n', ' ').Trim());
            return safe.Length <= 500 ? safe : safe[..500];
        }
        catch (JsonException)
        {
            return $"non_json_error_body(length={body.Length})";
        }
    }

    private static string RedactSensitive(string value)
    {
        var result = Regex.Replace(value, "(?i)bearer\\s+[a-z0-9._~+\\-/=]+", "Bearer [REDACTED]");
        result = Regex.Replace(result, "(?i)(api[_-]?key|secret|token)\\s*[:=]\\s*[^,;\\s]+", "$1=[REDACTED]");
        return result;
    }
}
