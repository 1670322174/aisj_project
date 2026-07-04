using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using InteriorDesignWeb.Data;
using InteriorDesignWeb.Models.DTOs;
using InteriorDesignWeb.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;

namespace InteriorDesignWeb.Services
{
    public class ComfyUIService
    {
        private readonly HttpClient _httpClient;
        private readonly FluxWorkflowService _workflowService;
        private readonly CosService _cosService;
        private readonly JobTrackingService _trackingService;
        private readonly ILogger<ComfyUIService> _logger;
        private readonly IConfiguration _config;
        private readonly string _imageBaseUrl;
        private readonly IServiceProvider _serviceProvider;
        private readonly IMemoryCache _cache;  // 引入 IMemoryCache
        private readonly DesignHubContext _context;  // 注入 DbContext 以访问数据库
        private readonly IServiceScopeFactory _scopeFactory;  // 引入 IServiceScopeFactory

        public ComfyUIService(
            IHttpClientFactory httpClientFactory,
            FluxWorkflowService workflowService,
            CosService cosService,
            JobTrackingService trackingService,
            ILogger<ComfyUIService> logger,
            IConfiguration config,
            IMemoryCache cache,
            IServiceProvider serviceProvider,
            DesignHubContext context,
            IServiceScopeFactory scopeFactory)  // 注入 IMemoryCache
        {
            _workflowService = workflowService;
            _cosService = cosService;
            _trackingService = trackingService;
            _logger = logger;
            _config = config;
            _cache = cache;  // 将 IMemoryCache 存储为私有字段
            _serviceProvider = serviceProvider;  // 注入 IServiceProvider 以创建新的服务作用域
            _context = context;  // 注入 DbContext 以访问数据库

            _httpClient = httpClientFactory.CreateClient("ComfyUI");
            _httpClient.BaseAddress = new Uri(config["ComfyUI:ApiUrl"]);
            _httpClient.Timeout = TimeSpan.FromMinutes(10);

            _imageBaseUrl = config["ComfyUI:ImageUrl"];
            _scopeFactory = scopeFactory;
        }
        //提交任务 调用工作流？
        public async Task<string> SubmitGenerationJob(FluxGenerationParameters parameters)
        {
            try
            {
                var workflow = _workflowService.GenerateWorkflow(parameters);
                var requestData = new { prompt = workflow };
                var jsonContent = JsonConvert.SerializeObject(requestData);
                _logger.LogDebug("提交工作流: {Workflow}", jsonContent);

                var response = await _httpClient.PostAsync("", new StringContent(jsonContent, Encoding.UTF8, "application/json"));
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new ComfyUIException($"ComfyUI API调用失败: {response.StatusCode} - {errorContent}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var responseData = JsonConvert.DeserializeObject<ComfyResponse>(responseContent);

                if (string.IsNullOrEmpty(responseData?.prompt_id))
                    throw new ComfyUIException("无效的ComfyUI响应");

                var jobId = Guid.NewGuid().ToString();

                // 1. 将任务状态存储在数据库中
                var job = new AiGenerationJob
                {
                    JobId = jobId,
                    PromptId = responseData.prompt_id,
                    Status = "processing",
                    ParametersJson = JsonConvert.SerializeObject(parameters),
                    CreatedAt = DateTime.UtcNow
                };
                _context.aigenerationjobs.Add(job);
                await _context.SaveChangesAsync();

                // 2. 将任务状态存储在内存缓存中，初始为 "processing"
                _cache.Set(jobId, new JobStatus { Status = "processing", Progress = 0 });

                // 启动任务监控，并传递 batch_size 参数
                _ = MonitorJobStatus(jobId, responseData.prompt_id, parameters.BatchSize);  // 传递 batch_size

                return jobId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "提交生成任务失败");
                throw;
            }
        }
        //监控 获取生成图片、缩略图 调用同步数据库
        private async Task MonitorJobStatus(string jobId, string promptId, int batchSize)
        {
            const int maxAttempts = 60;
            var attempt = 0;
            var jobImages = new List<AiGenerationJobImage>();  // 存储所有上传的图片

            while (attempt < maxAttempts)
            {
                attempt++;
                await Task.Delay(3000); // 每3秒检查一次任务状态

                // 在每次检查之前，检查任务是否已经被中断
                var jobStatus = _cache.Get<JobStatus>(jobId);
                if (jobStatus != null && jobStatus.Status == "interrupted")
                {
                    _logger.LogInformation($"任务 {jobId} 已被中断，停止监控。");
                    break; // 任务被中断，退出循环
                }

                try
                {
                    // 请求任务历史状态，获取生成的图片
                    var response = await _httpClient.GetAsync($"/history/{promptId}");
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning($"无法获取任务历史信息: {response.StatusCode}");
                        continue;
                    }

                    var responseContent = await response.Content.ReadAsStringAsync();
                    var history = JsonConvert.DeserializeObject<ComfyHistory>(responseContent);
                    // 记录原始的ComfyUI JSON响应，便于调试
                    _logger.LogDebug($"[ComfyUI JSON] jobId={jobId}, promptId={promptId}, content:\n{responseContent}");

                    if (history != null && history.ContainsKey(promptId))
                    {
                        var jobOutput = history[promptId].outputs;

                        // 检查是否返回了足够的图片
                        if (jobOutput == null || jobOutput.Count == 0)
                        {
                            _logger.LogWarning($"未找到图片输出，尝试重新获取...");
                            continue;
                        }

                        int totalCount = 0;

                        foreach (var output in jobOutput)
                        {
                            var images = output.Value.images;
                            if (images == null) continue;

                            foreach (var imageData in images)
                            {
                                if (totalCount >= batchSize) break;

                                var imageUrl = $"{_imageBaseUrl}{imageData.filename}&type={imageData.type}";
                                _logger.LogInformation($"[Image Handling] index={totalCount}, filename={imageData.filename}, url={imageUrl}");

                                var imageResponse = await _httpClient.GetAsync(imageUrl);
                                if (!imageResponse.IsSuccessStatusCode)
                                {
                                    _logger.LogError($"[Download Failed] URL={imageUrl}, Status={imageResponse.StatusCode}");
                                    continue;
                                }

                                using var imageStream = await imageResponse.Content.ReadAsStreamAsync();
                                var cosResult = await _cosService.UploadAIImageAsync(imageStream, imageData.filename, "AI-Generated");

                                var jobImage = new AiGenerationJobImage
                                {
                                    JobId = jobId,
                                    ImageUrl = cosResult.Url,
                                    CosPath = cosResult.Path,
                                    ThumbnailPath = cosResult.Path.Replace(
                                        Path.GetFileNameWithoutExtension(cosResult.Path),
                                        $"{Path.GetFileNameWithoutExtension(cosResult.Path)}_thumbnail"
                                    ),
                                    CreatedAt = DateTime.UtcNow
                                };

                                jobImages.Add(jobImage);
                                totalCount++;

                                // 生成并上传缩略图
                                try
                                {
                                    _logger.LogInformation("开始生成并上传缩略图...");
                                    var thumbnailPath = await _cosService.GenerateAndUploadThumbnailAsync(imageStream, cosResult.Path);
                                    jobImage.ThumbnailPath = thumbnailPath;  // 更新缩略图路径
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "生成并上传缩略图失败");
                                }
                            }

                            if (totalCount >= batchSize) break; // 全部生成完成后跳出外层循环
                        }

                        // 更新任务状态到数据库
                        if (jobImages.Count > 0)
                        {
                            await SyncJobStatusToDatabase(jobId, jobImages);  // 同步任务状态到数据库

                            // 更新缓存中的任务状态
                            _cache.Set(jobId, new JobStatus
                            {
                                Status = "completed",
                                Progress = 100
                            });

                            _logger.LogInformation($"任务 {jobId} 完成，成功上传 {jobImages.Count} 张图片");
                        }

                        return;  // 图片处理完成，退出函数
                    }
                    else
                    {
                        _logger.LogWarning($"无法解析历史记录或任务未生成图片，继续重试...");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "监控任务状态时出错");
                }

                // 超时处理
                if (attempt == maxAttempts)
                {
                    _cache.Set(jobId, new JobStatus { Status = "timeout", Progress = 100 });
                    await SyncJobStatusToDatabase(jobId, null);  // 超时保存任务状态
                    _logger.LogWarning($"任务 {jobId} 超时，未能完成生成");
                }
            }
        }

        // 新增：同步任务状态到数据库
        private async Task SyncJobStatusToDatabase(string jobId, List<AiGenerationJobImage> jobImages)
        {
            using (var scope = _scopeFactory.CreateScope())  // 创建新的作用域
            {
                var context = scope.ServiceProvider.GetRequiredService<DesignHubContext>();

                var jobStatus = _cache.Get<JobStatus>(jobId);

                if (jobStatus != null)
                {
                    var job = await context.aigenerationjobs
                        .FirstOrDefaultAsync(j => j.JobId == jobId);

                    if (job != null)
                    {
                        job.Status = jobStatus.Status;
                        job.Progress = jobStatus.Progress.ToString();
                        job.UpdatedAt = DateTime.UtcNow;

                        // 如果 jobImages 不为 null，插入生成的图片信息
                        if (jobImages != null)
                        {
                            foreach (var jobImage in jobImages)
                            {
                                context.aigenerationjobimages.Add(jobImage);
                            }
                        }

                        await context.SaveChangesAsync();  // 保存到数据库
                    }
                    else
                    {
                        _logger.LogWarning($"任务 {jobId} 不存在，无法更新数据库");
                    }
                }
            }
        }
        // 中断任务的方法
        public async Task<bool> InterruptJobAsync(string jobId)
        {
            try
            {
                // 1. 根据 jobId 获取对应的 PromptId
                var promptId = await GetPromptIdFromJobId(jobId);
                if (string.IsNullOrEmpty(promptId))
                {
                    _logger.LogError($"无法找到对应的 PromptId，任务 {jobId} 无法中断");
                    return false;
                }

                // 2. 使用 PromptId 调用 ComfyUI 中断接口
                var response = await _httpClient.PostAsync(
                    "/interrupt",
                    new StringContent($"{{\"prompt_id\":\"{promptId}\"}}", Encoding.UTF8, "application/json")
                );

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation($"任务中断成功: {result}");

                    // 3. 中断任务后，更新缓存中的任务状态为 "interrupted"
                    await UpdateJobStatus(jobId, "interrupted", 0); // 更新进度为 0

                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"中断任务失败: {errorContent}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "中断任务失败");
                return false;
            }
        }
        // 更新任务状态
        private async Task UpdateJobStatus(string jobId, string status, int progress)
        {
            try
            {
                // 更新缓存中的任务状态
                _cache.Set(jobId, new JobStatus
                {
                    Status = status,
                    Progress = progress
                });

                _logger.LogInformation($"缓存更新成功，任务 {jobId} 状态={status}, 进度={progress}%");

                // 更新数据库中的任务状态
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<DesignHubContext>();

                    var job = await context.aigenerationjobs
                        .FirstOrDefaultAsync(j => j.JobId == jobId);

                    if (job != null)
                    {
                        job.Status = status;
                        job.Progress = progress.ToString();
                        job.UpdatedAt = DateTime.UtcNow;
                        await context.SaveChangesAsync();
                        _logger.LogInformation($"数据库更新成功，任务 {jobId} 状态={status}, 进度={progress}%");
                    }
                    else
                    {
                        _logger.LogWarning($"任务 {jobId} 在数据库中未找到，无法更新状态");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"更新任务 {jobId} 状态失败");
            }
        }
        //查找任务
        private async Task<string> GetPromptIdFromJobId(string jobId)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<DesignHubContext>();

                // 根据 jobId 查找对应的任务
                var job = await context.aigenerationjobs
                    .FirstOrDefaultAsync(j => j.JobId == jobId);

                if (job != null)
                {
                    return job.PromptId;  // 返回对应的 PromptId
                }
                else
                {
                    _logger.LogError($"任务 {jobId} 未在数据库中找到");
                    return null;
                }
            }
        }

        // 添加一个新的方法，用于获取单个图片信息
        public async Task<ActionResult<AiGenerationJobImage>> GetImageByAiImageID(int aiImageID)
        {
            var aiImage = await _context.aigenerationjobimages
                .Where(i => i.AiImageID == aiImageID)
                .FirstOrDefaultAsync();

            if (aiImage == null)
                // Replace the problematic code with the following:
                return new NotFoundObjectResult(new { Code = "IMAGE_NOT_FOUND", Message = "图片不存在" });

            // Replace the problematic code with the following:
            return new OkObjectResult(new
            {
                AiImageID = aiImage.AiImageID,
                ImageUrl = aiImage.ImageUrl,
                CosPath = aiImage.CosPath,
                ThumbnailPath = aiImage.ThumbnailPath,
                CreatedAt = aiImage.CreatedAt.ToString("o")
            });
        }
    }


    public class JobStatus
    {
        public string Status { get; set; } // processing, completed, timeout
        public int Progress { get; set; }  // 任务进度（0 - 100）
        public FluxGenerationParameters Parameters { get; set; }
        public string ComfyPromptId { get; set; }
        public string ImageUrl { get; set; }  // COS 访问 URL
        public string CosPath { get; set; }   // COS 存储路径
        public string ImageFilename { get; set; }
        public string ImageType { get; set; }
        public string ThumbnailPath { get; set; } // 缩略图路径
        public string Error { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class ComfyUIException : Exception
    {
        public ComfyUIException(string message) : base(message) { }
        public ComfyUIException(string message, Exception inner) : base(message, inner) { }
    }

    public class ComfyResponse
    {
        public string prompt_id { get; set; }
    }

    public class ComfyHistory : Dictionary<string, ComfyJobOutput> { }

    public class ComfyJobOutput
    {
        public Dictionary<string, ComfyNodeOutput> outputs { get; set; }
    }

    public class ComfyNodeOutput
    {
        public List<ComfyImage> images { get; set; }
    }

    public class ComfyImage
    {
        public string filename { get; set; }
        public string type { get; set; }
        public string subfolder { get; set; }
    }
}
