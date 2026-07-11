// 作用：封装 Comfy Cloud API 调用。
// 负责上传输入文件、提交工作流、查询云端任务、下载输出文件和取消任务；不处理数据库或 COS 业务。

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using InteriorDesignWeb.Application.Common;
using InteriorDesignWeb.Config;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace InteriorDesignWeb.Providers.AI;

public sealed class ComfyCloudProvider : IAIProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HttpClient _httpClient;
    private readonly ComfyCloudOptions _options;
    private readonly ILogger<ComfyCloudProvider> _logger;

    public string ProviderType => "ComfyCloud";

    public ComfyCloudProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<ComfyCloudOptions> options,
        ILogger<ComfyCloudProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _httpClient = httpClientFactory.CreateClient("ComfyCloud");
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AIProviderSubmitResult> SubmitAsync(
        JObject workflow,
        CancellationToken cancellationToken = default)
    {
        // Partner Nodes 除了 X-API-Key 请求头，还要求在 extra_data 中携带同一个 Comfy API Key。
        var requestData = new
        {
            prompt = workflow,
            extra_data = new
            {
                api_key_comfy_org = _options.ApiKey
            }
        };

        var response = await _httpClient.PostAsync(
            "api/prompt",
            CreateJsonContent(requestData),
            cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Comfy Cloud 提交失败。StatusCode={StatusCode}, RetryAfter={RetryAfter}, Response={Response}",
                (int)response.StatusCode,
                response.Headers.RetryAfter?.ToString(),
                content);
        }

        EnsureSuccess(response, content, "提交工作流");

        var payload = JObject.Parse(content);
        var promptId = payload["prompt_id"]?.ToString();
        if (string.IsNullOrWhiteSpace(promptId))
        {
            throw new AppException(
                ErrorCodes.AiProviderError,
                "Comfy Cloud 响应缺少 prompt_id。",
                StatusCodes.Status502BadGateway);
        }

        return new AIProviderSubmitResult
        {
            ProviderJobId = promptId
        };
    }

    public async Task<AIProviderHistoryResult> GetHistoryAsync(
        string providerJobId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            $"api/jobs/{Uri.EscapeDataString(providerJobId)}",
            cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        // 云端任务提交后可能短暂处于尚未可查询状态，将 404 视作仍在排队，而不是业务失败。
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new AIProviderHistoryResult
            {
                Status = "pending",
                ProgressValue = 2,
                RawJson = content
            };
        }

        EnsureSuccess(response, content, "查询任务");

        var payload = JObject.Parse(content);
        var status = NormalizeStatus(payload["status"]?.ToString());
        var outputsNode = payload["outputs"] as JObject;
        var outputs = outputsNode == null
            ? Array.Empty<AIProviderOutput>()
            : ExtractOutputs(outputsNode);

        var executionError = payload["execution_error"] as JObject;
        var errorMessage = executionError?["exception_message"]?.ToString()
            ?? payload["error_message"]?.ToString();

        return new AIProviderHistoryResult
        {
            Status = status,
            IsCompleted = status == "completed",
            IsFailed = status is "failed" or "error",
            IsCancelled = status == "cancelled",
            ProgressValue = MapProgress(status),
            ErrorMessage = errorMessage,
            RawJson = content,
            Outputs = outputs
        };
    }

    public async Task<AIProviderUploadResult> UploadImageAsync(
        IFormFile file,
        string? subfolder = null,
        bool overwrite = true,
        CancellationToken cancellationToken = default)
    {
        if (file.Length <= 0)
        {
            throw AppException.Validation("上传文件不能为空。");
        }

        if (file.Length > 50L * 1024L * 1024L)
        {
            throw AppException.Validation("Comfy Cloud 单个上传文件不能超过 50MB。");
        }

        using var form = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream();
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(file.ContentType)
                ? "application/octet-stream"
                : file.ContentType);

        form.Add(fileContent, "image", file.FileName);
        form.Add(new StringContent("input"), "type");
        form.Add(new StringContent(overwrite ? "true" : "false"), "overwrite");

        // Cloud 接口为兼容本地 API 接受 subfolder，但云端实际使用平面内容寻址存储。
        if (!string.IsNullOrWhiteSpace(subfolder))
        {
            form.Add(new StringContent(subfolder), "subfolder");
        }

        var response = await _httpClient.PostAsync("api/upload/image", form, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, content, "上传输入图片");

        var payload = JObject.Parse(content);
        return new AIProviderUploadResult
        {
            Name = payload["name"]?.ToString() ?? file.FileName,
            Subfolder = payload["subfolder"]?.ToString() ?? string.Empty,
            Type = payload["type"]?.ToString() ?? "input"
        };
    }

    public async Task<Stream> DownloadOutputAsync(
        AIProviderOutput output,
        CancellationToken cancellationToken = default)
    {
        var requestUrl = BuildViewUrl(output);
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        // Cloud /api/view 通常返回 302 到短期有效的签名存储 URL。
        if (IsRedirect(response.StatusCode) && response.Headers.Location != null)
        {
            var signedUrl = response.Headers.Location.IsAbsoluteUri
                ? response.Headers.Location
                : new Uri(_httpClient.BaseAddress!, response.Headers.Location);

            // 请求签名 URL 时不能继续携带 X-API-Key。
            var unsignedClient = _httpClientFactory.CreateClient(string.Empty);
            var signedResponse = await unsignedClient.GetAsync(
                signedUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!signedResponse.IsSuccessStatusCode)
            {
                var signedContent = await signedResponse.Content.ReadAsStringAsync(cancellationToken);
                signedResponse.Dispose();
                throw CreateProviderException("下载云端输出", signedResponse.StatusCode, signedContent);
            }

            return await CopyToMemoryStreamAsync(signedResponse, cancellationToken);
        }

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            throw CreateProviderException("获取云端输出地址", response.StatusCode, content);
        }

        return await CopyToMemoryStreamAsync(response, cancellationToken);
    }

    public async Task<bool> CancelAsync(
        string providerJobId,
        CancellationToken cancellationToken = default)
    {
        var statusResponse = await _httpClient.GetAsync(
            $"api/job/{Uri.EscapeDataString(providerJobId)}/status",
            cancellationToken);

        var statusContent = await statusResponse.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(statusResponse, statusContent, "查询取消前任务状态");

        var status = NormalizeStatus(JObject.Parse(statusContent)["status"]?.ToString());

        if (status is "pending" or "waiting_to_dispatch")
        {
            var queueResponse = await _httpClient.PostAsync(
                "api/queue",
                CreateJsonContent(new { delete = new[] { providerJobId } }),
                cancellationToken);

            return queueResponse.IsSuccessStatusCode;
        }

        if (status == "in_progress")
        {
            // Comfy Cloud 的 interrupt 接口按当前账号中断正在运行的任务，不接受单独 prompt_id。
            var interruptResponse = await _httpClient.PostAsync(
                "api/interrupt",
                content: null,
                cancellationToken);

            return interruptResponse.IsSuccessStatusCode;
        }

        return status == "cancelled";
    }

    private static StringContent CreateJsonContent(object value)
    {
        return new StringContent(
            JsonConvert.SerializeObject(value),
            Encoding.UTF8,
            "application/json");
    }

    private static async Task<Stream> CopyToMemoryStreamAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        using (response)
        {
            var memoryStream = new MemoryStream();
            await response.Content.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;
            return memoryStream;
        }
    }

    private static bool IsRedirect(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.MovedPermanently
            or HttpStatusCode.Redirect
            or HttpStatusCode.RedirectMethod
            or HttpStatusCode.TemporaryRedirect
            or HttpStatusCode.PermanentRedirect;
    }

    private static string BuildViewUrl(AIProviderOutput output)
    {
        var type = string.IsNullOrWhiteSpace(output.Type) ? "output" : output.Type;
        return "api/view"
            + $"?filename={Uri.EscapeDataString(output.FileName)}"
            + $"&subfolder={Uri.EscapeDataString(output.Subfolder ?? string.Empty)}"
            + $"&type={Uri.EscapeDataString(type)}";
    }

    private static string NormalizeStatus(string? status)
    {
        return string.IsNullOrWhiteSpace(status)
            ? "pending"
            : status.Trim().ToLowerInvariant();
    }

    private static int MapProgress(string status)
    {
        return status switch
        {
            "waiting_to_dispatch" => 2,
            "pending" => 5,
            "in_progress" => 50,
            "completed" => 100,
            "failed" or "error" or "cancelled" => 100,
            _ => 5
        };
    }

    private static IReadOnlyList<AIProviderOutput> ExtractOutputs(JObject outputsNode)
    {
        var outputs = new List<AIProviderOutput>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in outputsNode.Properties())
        {
            ExtractFileObjects(node.Value, node.Name, null, outputs, seen);
        }

        return outputs;
    }

    private static void ExtractFileObjects(
        JToken token,
        string nodeId,
        string? propertyName,
        List<AIProviderOutput> outputs,
        HashSet<string> seen)
    {
        if (token is JObject obj)
        {
            var fileName = obj["filename"]?.ToString();
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                var subfolder = obj["subfolder"]?.ToString() ?? string.Empty;
                var type = obj["type"]?.ToString() ?? "output";
                var identity = $"{fileName}|{subfolder}|{type}";

                if (seen.Add(identity))
                {
                    outputs.Add(new AIProviderOutput
                    {
                        NodeId = nodeId,
                        FileName = fileName,
                        Subfolder = subfolder,
                        Type = type,
                        MediaType = InferMediaType(propertyName, fileName)
                    });
                }

                return;
            }

            foreach (var child in obj.Properties())
            {
                ExtractFileObjects(child.Value, nodeId, child.Name, outputs, seen);
            }

            return;
        }

        if (token is JArray array)
        {
            foreach (var child in array)
            {
                ExtractFileObjects(child, nodeId, propertyName, outputs, seen);
            }
        }
    }

    private static string InferMediaType(string? propertyName, string fileName)
    {
        var name = propertyName?.ToLowerInvariant() ?? string.Empty;
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (name.Contains("video") || name.Contains("gif")
            || extension is ".mp4" or ".webm" or ".mov" or ".avi" or ".gif")
        {
            return "video";
        }

        if (name.Contains("audio") || extension is ".mp3" or ".wav" or ".ogg" or ".m4a")
        {
            return "audio";
        }

        return "image";
    }

    private static void EnsureSuccess(
        HttpResponseMessage response,
        string content,
        string operation)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        throw CreateProviderException(operation, response.StatusCode, content);
    }

    private static AppException CreateProviderException(
        string operation,
        HttpStatusCode statusCode,
        string content)
    {
        var message = statusCode switch
        {
            HttpStatusCode.Unauthorized => "Comfy Cloud API Key 无效或未配置。",
            HttpStatusCode.PaymentRequired => "Comfy Cloud Credits 不足。",
            HttpStatusCode.TooManyRequests => "Comfy Cloud 套餐不可用或请求过于频繁。",
            _ => $"Comfy Cloud {operation}失败：{(int)statusCode} {statusCode} - {content}"
        };

        return new AppException(
            ErrorCodes.AiProviderError,
            message,
            StatusCodes.Status502BadGateway);
    }
}
