using InteriorDesignWeb.Data;
using InteriorDesignWeb.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace InteriorDesignWeb.Services
{
    /// <summary>
    /// AI生成任务服务，用于创建和管理AI生成任务的逻辑。
    /// </summary>
    public class JobTrackingService
    {
        private readonly DesignHubContext _context;
        private readonly ILogger<JobTrackingService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public JobTrackingService(
            DesignHubContext context,
            ILogger<JobTrackingService> logger,
            IServiceProvider serviceProvider)
        {
            _context = context;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task<AiGenerationJob> CreateJobAsync(
            string promptId,
            FluxGenerationParameters parameters)
        {
            using (var scope = _serviceProvider.CreateScope())  // 创建新的作用域
            {
                var context = scope.ServiceProvider.GetRequiredService<DesignHubContext>();

                var job = new AiGenerationJob
                {
                    JobId = Guid.NewGuid().ToString(),
                    PromptId = promptId,
                    ParametersJson = JsonConvert.SerializeObject(parameters),
                    Status = "processing",
                    CreatedAt = DateTime.UtcNow
                };

                context.aigenerationjobs.Add(job);
                await context.SaveChangesAsync();

                // 创建 AiGenerationJobImage 记录
                var jobImage = new AiGenerationJobImage
                {
                    JobId = job.JobId,  // 将 JobId 关联到 AiGenerationJobImage
                    ImageUrl = null,     // 这里需要根据具体的业务需求来设定
                    CosPath = null,      // 同上
                    ThumbnailPath = null,
                    CreatedAt = DateTime.UtcNow
                };

                context.aigenerationjobimages.Add(jobImage);  // 添加到 AiGenerationJobImages 表
                await context.SaveChangesAsync();

                _logger.LogInformation($"创建AI生成任务: JobId={job.JobId}, PromptId={promptId}");
                return job;
            }
        }

        public async Task<AiGenerationJob?> GetJobByJobIdAsync(string jobId)
        {
            // 查找 AiGenerationJob 对象
            var job = await _context.aigenerationjobs
                .Where(j => j.JobId == jobId)
                .FirstOrDefaultAsync();

            if (job == null)
                return null;

            // 获取所有与该任务相关的图片信息
            var jobImages = await _context.aigenerationjobimages
                .Where(i => i.JobId == jobId)
                .ToListAsync(); // 先获取到列表

            // 在内存中处理字符串操作
            var generatedImages = jobImages.Select(i => new GeneratedImageInfo
            {
                Filename = i.ImageUrl,
                Type = i.ImageUrl != null ? i.ImageUrl.Split('.').Last() : null,  // 在内存中处理
                Subfolder = i.CosPath,
            }).ToList();

            return new AiGenerationJob
            {
                JobId = job.JobId,
                PromptId = job.PromptId,
                Status = job.Status,
                ParametersJson = job.ParametersJson,
                GeneratedImages = generatedImages,  // 修复：将生成的图片信息正确映射为 GeneratedImageInfo 类型
                CreatedAt = job.CreatedAt,
                UpdatedAt = job.UpdatedAt
            };
        }
        public async Task<AiGenerationJob?> GetJobByPromptIdAsync(string promptId)
        {
            // 查找 AiGenerationJob 对象
            var job = await _context.aigenerationjobs
                .Where(j => j.PromptId == promptId)
                .FirstOrDefaultAsync();

            if (job == null)
                return null;

            // 获取所有与该任务相关的图片信息
            var jobImages = await _context.aigenerationjobimages
                .Where(i => i.JobId == job.JobId)
                .ToListAsync(); // 先获取到列表

            // 在内存中处理字符串操作
            var generatedImages = jobImages.Select(i => new GeneratedImageInfo
            {
                Filename = i.ImageUrl,
                Type = i.ImageUrl != null ? i.ImageUrl.Split('.').Last() : null,  // 在内存中处理
                Subfolder = i.CosPath,
            }).ToList();

            return new AiGenerationJob
            {
                JobId = job.JobId,
                PromptId = job.PromptId,
                Status = job.Status,
                ParametersJson = job.ParametersJson,
                GeneratedImages = generatedImages,  // 修复：将生成的图片信息正确映射为 GeneratedImageInfo 类型
                CreatedAt = job.CreatedAt,
                UpdatedAt = job.UpdatedAt
            };
        }


        public async Task UpdateJobStatusAsync(
            string jobId,
            string status,
            string progress = "0",  // 默认进度为 "0"
            string imageUrl = null,
            string cosPath = null,
            List<GeneratedImageInfo> generatedImages = null)
        {
            var job = await GetJobByJobIdAsync(jobId);
            if (job == null)
            {
                _logger.LogWarning($"更新任务状态失败: JobId={jobId} 不存在");
                return;
            }

            job.Status = status;
            job.Progress = progress;  // 更新进度
            job.UpdatedAt = DateTime.UtcNow;

            // 只有在有图片信息时，才更新 AiGenerationJobImage 表
            if (!string.IsNullOrEmpty(imageUrl) || !string.IsNullOrEmpty(cosPath))
            {
                var jobImage = new AiGenerationJobImage
                {
                    JobId = jobId,
                    ImageUrl = imageUrl,
                    CosPath = cosPath,
                    ThumbnailPath = cosPath?.Replace(Path.GetFileNameWithoutExtension(cosPath), $"{Path.GetFileNameWithoutExtension(cosPath)}_thumbnail"),
                    CreatedAt = DateTime.UtcNow
                };

                // 插入新的图片记录
                _context.aigenerationjobimages.Add(jobImage);
            }

            if (generatedImages != null)
            {
                // 处理生成的图片列表
                foreach (var generatedImage in generatedImages)
                {
                    var jobImage = new AiGenerationJobImage
                    {
                        JobId = jobId,
                        //ImageUrl = generatedImage.ImageUrl,
                        //CosPath = generatedImage.CosPath,
                        //ThumbnailPath = generatedImage.ThumbnailPath,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.aigenerationjobimages.Add(jobImage);
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation($"更新任务状态: JobId={jobId}, Status={status}, Progress={progress}");
        }
    }
}
