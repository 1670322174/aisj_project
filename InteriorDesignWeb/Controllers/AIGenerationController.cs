using InteriorDesignWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Configuration;
using System.Text;
using System.Text.Json;
using InteriorDesignWeb.Data;
using Microsoft.EntityFrameworkCore;

namespace InteriorDesignWeb.Controllers
{
    [ApiController]
    [Route("api/flux")]
    public class FluxGenerationController : ControllerBase
    {
        private readonly ComfyUIService _comfyService;
        private readonly ILogger<FluxGenerationController> _logger;
        private readonly JobTrackingService _trackingService;
        private readonly CosService _cosService;
        private readonly IMemoryCache _cache;  // 引入 IMemoryCache
        private readonly DesignHubContext _context;

        public FluxGenerationController(
            ComfyUIService comfyService,
            ILogger<FluxGenerationController> logger,
            JobTrackingService trackingService,
            CosService cosService,
            IMemoryCache cache,
            DesignHubContext context)
        {
            _comfyService = comfyService;
            _logger = logger;
            _trackingService = trackingService;
            _cosService = cosService;
            _cache = cache;  // 初始化 IMemoryCache
            _context = context;
        }
        /// <summary>
        /// 提交AI图像生成任务到ComfyUI。返回生成的图像ID。
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [HttpPost("generate")]
        public async Task<IActionResult> GenerateImage([FromBody] FluxGenerationParameters parameters)
        {
            try
            {
                var jobId = await _comfyService.SubmitGenerationJob(parameters);
                return Ok(new { JobId = jobId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "图片生成任务提交失败");
                return StatusCode(500, new { Code = "SUBMIT_FAILED", Message = ex.Message });
            }
        }
        /// <summary>
        /// 获取任务状态。
        /// </summary>
        /// <param name="jobId"></param>
        /// <returns></returns>
        [HttpGet("status/{jobId}")]
        public IActionResult GetJobStatus(string jobId)
        {
            var jobStatus = _cache.Get<JobStatus>(jobId);
            if (jobStatus == null)
            {
                return NotFound(new { Code = "JOB_NOT_FOUND", Message = "任务不存在" });
            }

            return Ok(new
            {
                jobStatus.Status,
                jobStatus.Progress,
                jobStatus.ImageUrl,
                jobStatus.CosPath
            });
        }

        // 新增：获取生成的图片信息-------------------无用------------------------
        [HttpGet("generated-image/{jobId}")]
        public async Task<IActionResult> GetGeneratedImage(string jobId)
        {
            try
            {
                var job = await _trackingService.GetJobByJobIdAsync(jobId);
                if (job == null)
                    return NotFound(new { Code = "JOB_NOT_FOUND", Message = "任务不存在" });

                if (job.Status != "completed")
                    return BadRequest(new { Code = "JOB_NOT_COMPLETED", Message = "任务尚未完成" });

                if (job.GeneratedImages == null || !job.GeneratedImages.Any())
                    return NotFound(new { Code = "IMAGE_NOT_FOUND", Message = "没有生成的图片" });

                // 返回所有与该任务相关的图片信息
                return Ok(new
                {
                    JobId = job.JobId,
                    Images = job.GeneratedImages  // 返回生成的所有图片信息
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取生成图片失败: {JobId}", jobId);
                return StatusCode(500, new { Code = "IMAGE_FAILED", Message = ex.Message });
            }
        }

        // 新增：通过prompt_id获取任务状态
        [HttpGet("statusByPrompt/{promptId}")]
        public async Task<IActionResult> GetJobStatusByPromptId(string promptId)
        {
            try
            {
                var job = await _trackingService.GetJobByPromptIdAsync(promptId);
                if (job == null)
                    return NotFound(new { Code = "JOB_NOT_FOUND", Message = "任务不存在" });

                return Ok(new
                {
                    job.JobId,
                    job.PromptId,
                    job.Status,

                    GeneratedImages = job.GeneratedImages,
                    CreatedAt = job.CreatedAt.ToString("o"),
                    UpdatedAt = job.UpdatedAt?.ToString("o")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "通过PromptId获取任务状态失败: {PromptId}", promptId);
                return StatusCode(500, new { Code = "STATUS_FAILED", Message = ex.Message });
            }
        }
        // 新增：中断任务
        [HttpPost("interrupt/{promptId}")]
        public async Task<IActionResult> InterruptJob(string promptId)
        {
            try
            {
                bool isInterrupted = await _comfyService.InterruptJobAsync(promptId);
                if (isInterrupted)
                {
                    return Ok(new { success = true, message = "任务已中断" });
                }
                return BadRequest(new { success = false, message = "中断任务失败" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "任务中断失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// 获取指定任务ID的所有图片信息。
        /// </summary>
        /// <param name="jobId"></param>
        /// <returns></returns>
        [HttpGet("job-image/{jobId}")]
        public async Task<IActionResult> GetImagesByJobId(string jobId)
        {
            var images = await _context.aigenerationjobimages
                .Where(i => i.JobId == jobId)
                .Select(i => new
                {
                    i.AiImageID,
                    i.ImageUrl,
                    i.CosPath,
                    i.ThumbnailPath,
                    i.CreatedAt
                })
                .ToListAsync();

            return Ok(images);
        }

        // 新增：通过 AiImageID 获取图片信息
        [HttpGet("generated-image-info/{aiImageID}")]
        public async Task<IActionResult> GetGeneratedImageInfo(int aiImageID)
        {
            try
            {
                var aiImage = await _context.aigenerationjobimages
                    .Where(i => i.AiImageID == aiImageID)
                    .FirstOrDefaultAsync();

                if (aiImage == null)
                    return NotFound(new { Code = "IMAGE_NOT_FOUND", Message = "AI生成的图片不存在" });

                return Ok(new
                {
                    AiImageID = aiImage.AiImageID,
                    ImageUrl = aiImage.ImageUrl,
                    CosPath = aiImage.CosPath,
                    ThumbnailPath = aiImage.ThumbnailPath,
                    CreatedAt = aiImage.CreatedAt.ToString("o")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取图片信息失败: {AiImageID}", aiImageID);
                return StatusCode(500, new { Code = "IMAGE_FAILED", Message = ex.Message });
            }
        }
        [HttpGet("generatedimage/{aiImageId}")]
        public async Task<IActionResult> GetImageByAiImageId(int aiImageId, [FromQuery] string type = "original")
        {
            try
            {
                // 调用CosService获取图像流
                var (imageStream, contentType) = await _cosService.GetImageStreamByAiImageIdAsync(aiImageId, type);

                // 返回图像流
                return File(imageStream, contentType);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogError(ex, "未找到图片");
                return NotFound(new { Code = "IMAGE_NOT_FOUND", Message = "图片未找到" });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "无效的图片路径");
                return BadRequest(new { Code = "INVALID_PATH", Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取图片失败");
                return StatusCode(500, new { Code = "IMAGE_FAILED", Message = ex.Message });
            }
        }
        public class JobResponse
        {
            public required string JobId { get; set; }
            public required string Message { get; set; }
        }

        public class ErrorResponse
        {
            public required string Code { get; set; }
            public required string Message { get; set; }
            public required string Detail { get; set; }
            public required List<string> Errors { get; set; }
        }
    }
}
