// 作用：封装 ComfyUI API 调用，包括提交工作流、查询历史、上传输入图、下载输出文件和取消任务。
// 本类只处理 ComfyUI 协议细节，不负责数据库写入、COS 上传或业务权限判断。

using System.Net.Http.Headers;
using System.Text;
using InteriorDesignWeb.Application.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.AspNetCore.Http;

namespace InteriorDesignWeb.Providers.AI;

public class ComfyUIProvider : IAIProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ComfyUIProvider> _logger;
    private readonly string? _imageViewBaseUrl;

    public string ProviderType => "ComfyUI";

    public ComfyUIProvider(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ComfyUIProvider> logger)
    {
        _httpClient = httpClientFactory.CreateClient("ComfyUI");
        _imageViewBaseUrl = configuration["ComfyUI:ImageUrl"];
        _logger = logger;
    }

    public async Task<AIProviderSubmitResult> SubmitAsync(
        JObject workflow,
        CancellationToken cancellationToken = default)
    {
        var requestData = new { prompt = workflow };
        var json = JsonConvert.SerializeObject(requestData);

        var response = await _httpClient.PostAsync(
            string.Empty,
            new StringContent(json, Encoding.UTF8, "application/json"),
            cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new AppException(
                ErrorCodes.AiProviderError,
                $"ComfyUI 提交失败：{response.StatusCode} - {content}",
                StatusCodes.Status502BadGateway);
        }

        var payload = JObject.Parse(content);
        var promptId = payload["prompt_id"]?.ToString();
        if (string.IsNullOrWhiteSpace(promptId))
        {
            throw new AppException(
                ErrorCodes.AiProviderError,
                "ComfyUI 响应缺少 prompt_id。",
                StatusCodes.Status502BadGateway);
        }

        return new AIProviderSubmitResult { ProviderJobId = promptId };
    }

    public async Task<AIProviderHistoryResult> GetHistoryAsync(
        string providerJobId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/history/{Uri.EscapeDataString(providerJobId)}", cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "ComfyUI 历史查询失败。ProviderJobId={ProviderJobId}, Status={Status}",
                providerJobId,
                response.StatusCode);

            return new AIProviderHistoryResult
            {
                IsCompleted = false,
                RawJson = content,
                Outputs = Array.Empty<AIProviderOutput>()
            };
        }

        if (string.IsNullOrWhiteSpace(content) || content.Trim() == "{}")
        {
            return new AIProviderHistoryResult
            {
                IsCompleted = false,
                RawJson = content,
                Outputs = Array.Empty<AIProviderOutput>()
            };
        }

        var root = JObject.Parse(content);
        var jobNode = root[providerJobId] as JObject;
        var outputsNode = jobNode?["outputs"] as JObject;
        var outputs = outputsNode == null
            ? Array.Empty<AIProviderOutput>()
            : ExtractOutputs(outputsNode);

        return new AIProviderHistoryResult
        {
            IsCompleted = outputs.Count > 0,
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

        using var form = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream();
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);

        form.Add(fileContent, "image", file.FileName);
        form.Add(new StringContent("input"), "type");
        form.Add(new StringContent(overwrite ? "true" : "false"), "overwrite");

        if (!string.IsNullOrWhiteSpace(subfolder))
        {
            form.Add(new StringContent(subfolder), "subfolder");
        }

        var response = await _httpClient.PostAsync("/upload/image", form, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new AppException(
                ErrorCodes.AiProviderError,
                $"ComfyUI 图片上传失败：{response.StatusCode} - {content}",
                StatusCodes.Status502BadGateway);
        }

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
        var type = string.IsNullOrWhiteSpace(output.Type) ? "output" : output.Type;
        var url = BuildViewUrl(output.FileName, output.Subfolder, type);

        var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new AppException(
                ErrorCodes.AiProviderError,
                $"ComfyUI 输出下载失败：{response.StatusCode} - {content}",
                StatusCodes.Status502BadGateway);
        }

        var memoryStream = new MemoryStream();
        await response.Content.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;
        return memoryStream;
    }


    private string BuildViewUrl(string fileName, string subfolder, string type)
    {
        // 兼容项目现有配置：ComfyUI:ImageUrl 通常是 http://host/api/view?filename=
        if (!string.IsNullOrWhiteSpace(_imageViewBaseUrl))
        {
            var separator = _imageViewBaseUrl.Contains('?') ? "&" : "?";
            if (_imageViewBaseUrl.EndsWith("filename=", StringComparison.OrdinalIgnoreCase))
            {
                return $"{_imageViewBaseUrl}{Uri.EscapeDataString(fileName)}&subfolder={Uri.EscapeDataString(subfolder ?? string.Empty)}&type={Uri.EscapeDataString(type)}";
            }

            return $"{_imageViewBaseUrl}{separator}filename={Uri.EscapeDataString(fileName)}&subfolder={Uri.EscapeDataString(subfolder ?? string.Empty)}&type={Uri.EscapeDataString(type)}";
        }

        return $"/view?filename={Uri.EscapeDataString(fileName)}&subfolder={Uri.EscapeDataString(subfolder ?? string.Empty)}&type={Uri.EscapeDataString(type)}";
    }

    public async Task<bool> CancelAsync(
        string providerJobId,
        CancellationToken cancellationToken = default)
    {
        var json = JsonConvert.SerializeObject(new { prompt_id = providerJobId });
        var response = await _httpClient.PostAsync(
            "/interrupt",
            new StringContent(json, Encoding.UTF8, "application/json"),
            cancellationToken);

        return response.IsSuccessStatusCode;
    }

    private static IReadOnlyList<AIProviderOutput> ExtractOutputs(JObject outputsNode)
    {
        var outputs = new List<AIProviderOutput>();

        foreach (var node in outputsNode.Properties())
        {
            if (node.Value is not JObject nodeOutput)
            {
                continue;
            }

            AddOutputList(outputs, node.Name, nodeOutput["images"], "image");
            AddOutputList(outputs, node.Name, nodeOutput["videos"], "video");
            AddOutputList(outputs, node.Name, nodeOutput["gifs"], "video");
        }

        return outputs;
    }

    private static void AddOutputList(
        List<AIProviderOutput> outputs,
        string nodeId,
        JToken? token,
        string mediaType)
    {
        if (token is not JArray array)
        {
            return;
        }

        foreach (var item in array.OfType<JObject>())
        {
            var fileName = item["filename"]?.ToString();
            if (string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

            outputs.Add(new AIProviderOutput
            {
                NodeId = nodeId,
                FileName = fileName,
                Subfolder = item["subfolder"]?.ToString() ?? string.Empty,
                Type = item["type"]?.ToString() ?? "output",
                MediaType = mediaType
            });
        }
    }
}
