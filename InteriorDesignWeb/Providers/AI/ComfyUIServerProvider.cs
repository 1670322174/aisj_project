// 作用：封装本地或远程 ComfyUI Server API。
// 负责上传输入文件、提交工作流、查询队列/历史、下载输出和取消任务；不处理数据库、COS 或用户权限。

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

public sealed class ComfyUIServerProvider : IAIProvider
{
    private readonly HttpClient _httpClient;
    private readonly ComfyUIServerOptions _options;
    private readonly ILogger<ComfyUIServerProvider> _logger;

    public string ProviderType => "ComfyUIServer";

    public ComfyUIServerProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<ComfyUIServerOptions> options,
        ILogger<ComfyUIServerProvider> logger)
    {
        _httpClient = httpClientFactory.CreateClient("ComfyUI");
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AIProviderSubmitResult> SubmitAsync(
        JObject workflow,
        CancellationToken cancellationToken = default)
    {
        // Headless 调用 Partner Nodes 时，浏览器登录状态不会自动传给后端，必须显式携带 Account API Key。
        var requestData = new
        {
            prompt = workflow,
            client_id = _options.ClientId,
            extra_data = new
            {
                api_key_comfy_org = _options.AccountApiKey
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "prompt")
        {
            Content = CreateJsonContent(requestData)
        };
        using var response = await SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, content, "提交工作流");

        var payload = ParseObject(content, "ComfyUI 提交响应不是有效 JSON。");
        var promptId = payload["prompt_id"]?.ToString();
        if (string.IsNullOrWhiteSpace(promptId))
        {
            throw new AppException(
                ErrorCodes.AiProviderError,
                "ComfyUI 响应缺少 prompt_id。",
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
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"history/{Uri.EscapeDataString(providerJobId)}");
        using var response = await SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, content, "查询任务历史");

        if (string.IsNullOrWhiteSpace(content) || content.Trim() == "{}")
        {
            return await GetQueueStateAsync(providerJobId, content, cancellationToken);
        }

        var root = ParseObject(content, "ComfyUI 历史响应不是有效 JSON。");
        var jobNode = root[providerJobId] as JObject;

        // 兼容可能直接返回单个任务对象的代理实现。
        if (jobNode == null && (root["outputs"] != null || root["status"] != null))
        {
            jobNode = root;
        }

        if (jobNode == null)
        {
            return await GetQueueStateAsync(providerJobId, content, cancellationToken);
        }

        var outputsNode = jobNode["outputs"] as JObject;
        var outputs = outputsNode == null
            ? Array.Empty<AIProviderOutput>()
            : ExtractOutputs(outputsNode);

        var statusNode = jobNode["status"] as JObject;
        var statusText = statusNode?["status_str"]?.ToString()?.Trim().ToLowerInvariant();
        var completedFlag = statusNode?["completed"]?.Value<bool?>() == true;
        var messages = statusNode?["messages"];
        var errorMessage = ExtractExecutionMessage(messages, "execution_error");
        var wasInterrupted = HasExecutionMessage(messages, "execution_interrupted");

        if (!string.IsNullOrWhiteSpace(errorMessage)
            || statusText is "error" or "failed")
        {
            return new AIProviderHistoryResult
            {
                Status = "failed",
                IsFailed = true,
                ProgressValue = 100,
                ErrorMessage = errorMessage ?? "ComfyUI 工作流执行失败。",
                RawJson = content,
                Outputs = outputs
            };
        }

        if (wasInterrupted || statusText is "cancelled" or "canceled")
        {
            return new AIProviderHistoryResult
            {
                Status = "cancelled",
                IsCancelled = true,
                ProgressValue = 100,
                RawJson = content,
                Outputs = outputs
            };
        }

        if (completedFlag || statusText == "success" || outputs.Count > 0)
        {
            return new AIProviderHistoryResult
            {
                Status = "completed",
                IsCompleted = true,
                ProgressValue = 100,
                RawJson = content,
                Outputs = outputs
            };
        }

        return await GetQueueStateAsync(providerJobId, content, cancellationToken);
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

        if (file.Length > _options.MaxUploadBytes)
        {
            throw AppException.Validation(
                $"上传文件不能超过 {_options.MaxUploadBytes / 1024 / 1024}MB。");
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

        if (!string.IsNullOrWhiteSpace(subfolder))
        {
            form.Add(new StringContent(subfolder), "subfolder");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "upload/image")
        {
            Content = form
        };
        using var response = await SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, content, "上传输入图片");

        var payload = ParseObject(content, "ComfyUI 上传响应不是有效 JSON。");
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
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildViewUrl(output));
        using var response = await SendAsync(
            request,
            cancellationToken,
            HttpCompletionOption.ResponseHeadersRead);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            throw CreateProviderException("下载输出文件", response.StatusCode, content);
        }

        var memoryStream = new MemoryStream();
        await response.Content.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;
        return memoryStream;
    }

    public async Task<bool> CancelAsync(
        string providerJobId,
        CancellationToken cancellationToken = default)
    {
        // 新版 ComfyUI 支持按任务 ID 精确取消。旧版不存在该路由时，自动回退到 /queue + /interrupt。
        using (var directRequest = new HttpRequestMessage(
                   HttpMethod.Post,
                   $"api/jobs/{Uri.EscapeDataString(providerJobId)}/cancel"))
        using (var directResponse = await SendAsync(directRequest, cancellationToken))
        {
            if (directResponse.IsSuccessStatusCode)
            {
                var content = await directResponse.Content.ReadAsStringAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(content))
                {
                    return true;
                }

                var payload = ParseObject(content, "ComfyUI 取消响应不是有效 JSON。");
                return payload["cancelled"]?.Value<bool?>() ?? true;
            }

            if (directResponse.StatusCode is not HttpStatusCode.NotFound
                and not HttpStatusCode.MethodNotAllowed)
            {
                var content = await directResponse.Content.ReadAsStringAsync(cancellationToken);
                throw CreateProviderException("取消任务", directResponse.StatusCode, content);
            }
        }

        var queueState = await GetQueueClassificationAsync(providerJobId, cancellationToken);
        if (queueState == "pending")
        {
            using var queueRequest = new HttpRequestMessage(HttpMethod.Post, "queue")
            {
                Content = CreateJsonContent(new { delete = new[] { providerJobId } })
            };
            using var queueResponse = await SendAsync(queueRequest, cancellationToken);
            var content = await queueResponse.Content.ReadAsStringAsync(cancellationToken);
            EnsureSuccess(queueResponse, content, "从队列删除任务");
            return true;
        }

        if (queueState == "running")
        {
            using var interruptRequest = new HttpRequestMessage(HttpMethod.Post, "interrupt");
            using var interruptResponse = await SendAsync(interruptRequest, cancellationToken);
            var content = await interruptResponse.Content.ReadAsStringAsync(cancellationToken);
            EnsureSuccess(interruptResponse, content, "中断运行中任务");
            return true;
        }

        return false;
    }

    private async Task<AIProviderHistoryResult> GetQueueStateAsync(
        string providerJobId,
        string rawHistoryJson,
        CancellationToken cancellationToken)
    {
        var state = await GetQueueClassificationAsync(providerJobId, cancellationToken);
        return state switch
        {
            "running" => new AIProviderHistoryResult
            {
                Status = "in_progress",
                ProgressValue = 50,
                RawJson = rawHistoryJson
            },
            "pending" => new AIProviderHistoryResult
            {
                Status = "pending",
                ProgressValue = 5,
                RawJson = rawHistoryJson
            },
            _ => new AIProviderHistoryResult
            {
                // 刚提交后可能短暂既不在 history 也不在 queue，先视为排队中。
                Status = "pending",
                ProgressValue = 2,
                RawJson = rawHistoryJson
            }
        };
    }

    private async Task<string> GetQueueClassificationAsync(
        string providerJobId,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "queue");
        using var response = await SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, content, "查询执行队列");

        var root = ParseObject(content, "ComfyUI 队列响应不是有效 JSON。");
        if (ContainsPromptId(root["queue_running"], providerJobId))
        {
            return "running";
        }

        if (ContainsPromptId(root["queue_pending"], providerJobId))
        {
            return "pending";
        }

        return "unknown";
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken,
        HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
    {
        try
        {
            return await _httpClient.SendAsync(request, completionOption, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(
                ex,
                "ComfyUI Server 请求超时。Method={Method}, Path={Path}",
                request.Method,
                request.RequestUri);

            throw new AppException(
                ErrorCodes.AiProviderError,
                "连接 ComfyUI Server 超时，请检查服务器负载和网络。",
                StatusCodes.Status504GatewayTimeout);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "无法连接 ComfyUI Server。Method={Method}, Path={Path}",
                request.Method,
                request.RequestUri);

            throw new AppException(
                ErrorCodes.AiProviderError,
                "无法连接 ComfyUI Server，请检查地址、端口、防火墙和服务状态。",
                StatusCodes.Status502BadGateway);
        }
    }

    private static bool ContainsPromptId(JToken? token, string providerJobId)
    {
        if (token == null)
        {
            return false;
        }

        if (token is JObject obj)
        {
            var objectId = obj["prompt_id"]?.ToString()
                ?? obj["job_id"]?.ToString();
            if (string.Equals(objectId, providerJobId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return obj.Properties().Any(property => ContainsPromptId(property.Value, providerJobId));
        }

        if (token is JArray array)
        {
            // 标准 ComfyUI queue 条目结构中 prompt_id 位于索引 1。
            if (array.Count > 1
                && string.Equals(
                    array[1]?.ToString(),
                    providerJobId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return array.Any(item => ContainsPromptId(item, providerJobId));
        }

        return false;
    }

    private static string? ExtractExecutionMessage(JToken? messages, string messageType)
    {
        if (messages is not JArray messageArray)
        {
            return null;
        }

        foreach (var item in messageArray.OfType<JArray>())
        {
            if (item.Count < 2
                || !string.Equals(
                    item[0]?.ToString(),
                    messageType,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (item[1] is JObject details)
            {
                return details["exception_message"]?.ToString()
                    ?? details["message"]?.ToString()
                    ?? details.ToString(Formatting.None);
            }

            return item[1]?.ToString();
        }

        return null;
    }

    private static bool HasExecutionMessage(JToken? messages, string messageType)
    {
        if (messages is not JArray messageArray)
        {
            return false;
        }

        return messageArray.OfType<JArray>().Any(item =>
            item.Count > 0
            && string.Equals(
                item[0]?.ToString(),
                messageType,
                StringComparison.OrdinalIgnoreCase));
    }

    private static StringContent CreateJsonContent(object value)
    {
        return new StringContent(
            JsonConvert.SerializeObject(value),
            Encoding.UTF8,
            "application/json");
    }

    private static string BuildViewUrl(AIProviderOutput output)
    {
        var type = string.IsNullOrWhiteSpace(output.Type) ? "output" : output.Type;
        return "view"
            + $"?filename={Uri.EscapeDataString(output.FileName)}"
            + $"&subfolder={Uri.EscapeDataString(output.Subfolder ?? string.Empty)}"
            + $"&type={Uri.EscapeDataString(type)}";
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

    private static JObject ParseObject(string content, string errorMessage)
    {
        try
        {
            return JObject.Parse(content);
        }
        catch (JsonException)
        {
            throw new AppException(
                ErrorCodes.AiProviderError,
                errorMessage,
                StatusCodes.Status502BadGateway);
        }
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
        var safeContent = string.IsNullOrWhiteSpace(content)
            ? "无响应正文"
            : content.Length > 2000
                ? content[..2000] + "..."
                : content;

        var message = statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                => "ComfyUI Server 入口鉴权失败，请检查反向代理 AuthorizationHeader。",
            HttpStatusCode.NotFound
                => $"ComfyUI Server 不支持{operation}所需接口，请检查 ApiUrl 和 ComfyUI 版本。",
            HttpStatusCode.BadRequest
                => $"ComfyUI {operation}失败，工作流或参数校验未通过：{safeContent}",
            _ => $"ComfyUI {operation}失败：{(int)statusCode} {statusCode} - {safeContent}"
        };

        return new AppException(
            ErrorCodes.AiProviderError,
            message,
            StatusCodes.Status502BadGateway);
    }
}
