using InteriorDesignWeb.Data;
using InteriorDesignWeb.Models.Entities;
using InteriorDesignWeb.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using InteriorDesignWeb.Services;
using Microsoft.AspNetCore.Identity;
using System.Linq;

namespace InteriorDesignWeb.Controllers
{
    /*4/6*/
    [ApiController]
    [Route("api/projects/{projectId}/images")]
    [Authorize]
    public class ProjectImagesController : ControllerBase
    {
        private readonly DesignHubContext _context;
        private readonly ILogger<ProjectImagesController> _logger;
        private readonly IQuotaService _quotaService;
        private readonly IRoleLimitService _roleLimitService;
        private readonly CosService _cosService;

        public ProjectImagesController(
            DesignHubContext context,
            ILogger<ProjectImagesController> logger,
            IQuotaService quotaService,
            IRoleLimitService roleLimitService,
            CosService cosService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _quotaService = quotaService ?? throw new ArgumentNullException(nameof(quotaService));
            _roleLimitService = roleLimitService ?? throw new ArgumentNullException(nameof(roleLimitService));
            _cosService = cosService ?? throw new ArgumentNullException(nameof(cosService));
        }

        // 添加图片到方案房间
        [HttpPost]
        public async Task<IActionResult> AddImageToProject(int projectId, [FromBody] ProjectImageAddRequest request)
        {
            var userId = GetCurrentUserId();
            if (!await ValidateProjectAccess(projectId, userId))
                return Forbid();

            var imageId = request.ImageID.GetValueOrDefault();
            var aiImageId = request.AiImageID.GetValueOrDefault();
            if ((imageId > 0) == (aiImageId > 0))
            {
                return BadRequest("ImageID 和 AiImageID 必须且只能提供一个有效值");
            }

            var roomId = request.RoomID.GetValueOrDefault() > 0
                ? request.RoomID
                : null;
            if (roomId.HasValue)
            {
                var roomExists = await _context.projectrooms
                    .AnyAsync(room => room.RoomID == roomId.Value && room.ProjectID == projectId);

                if (!roomExists)
                    return BadRequest("房间不存在或不属于当前方案");
            }

            if (imageId > 0)
            {
                var imageExists = await _context.images
                    .AnyAsync(image => image.ImageID == imageId);
                if (!imageExists)
                    return BadRequest("图片不存在");

                var existingRelation = await _context.projectimages
                    .FirstOrDefaultAsync(item => item.ProjectID == projectId && item.ImageID == imageId);
                if (existingRelation != null)
                {
                    existingRelation.RoomID = roomId;
                    if (request.CustomTags != null)
                        existingRelation.CustomTags = request.CustomTags;
                    await _context.SaveChangesAsync();

                    return Ok(new
                    {
                        Message = "图片已在方案中",
                        AlreadyExists = true,
                        existingRelation.RelationID
                    });
                }

                await _context.projectimages.AddAsync(new ProjectImage
                {
                    ProjectID = projectId,
                    RoomID = roomId,
                    ImageID = imageId,
                    CustomTags = request.CustomTags,
                    SourceType = "gallery",
                    CreatedByUserID = userId
                });
                await _context.SaveChangesAsync();
            }
            else
            {
                var aiImage = await _context.aigenerationjobimages
                    .FirstOrDefaultAsync(image => image.AiImageID == aiImageId
                        && (image.UserID == userId
                            || (image.AiGenerationJob != null
                                && image.AiGenerationJob.UserID == userId)
                            || _context.projectimages.Any(relation =>
                                relation.AiImageID == image.AiImageID
                                && relation.Project != null
                                && relation.Project.UserID == userId)));
                if (aiImage == null)
                    return NotFound("AI生成的图片不存在或无权访问");

                var existingRelation = await _context.projectimages
                    .FirstOrDefaultAsync(item => item.ProjectID == projectId && item.AiImageID == aiImageId);
                if (existingRelation != null)
                {
                    existingRelation.RoomID = roomId;
                    if (request.CustomTags != null)
                        existingRelation.CustomTags = request.CustomTags;
                    aiImage.IsAddedToProject = true;
                    aiImage.UserID ??= userId;
                    aiImage.RetentionStatus = AiGenerationJobImage.RetentionRetained;
                    aiImage.CleanupEligibleAt = null;
                    await _context.SaveChangesAsync();

                    return Ok(new
                    {
                        Message = "AI图片已在方案中",
                        AlreadyExists = true,
                        existingRelation.RelationID
                    });
                }

                await _context.projectimages.AddAsync(new ProjectImage
                {
                    ProjectID = projectId,
                    RoomID = roomId,
                    AiImageID = aiImageId,
                    CustomTags = request.CustomTags,
                    SourceType = "ai",
                    CreatedByUserID = userId
                });

                aiImage.IsAddedToProject = true;
                aiImage.UserID ??= userId;
                aiImage.RetentionStatus = AiGenerationJobImage.RetentionRetained;
                aiImage.CleanupEligibleAt = null;
                await _context.SaveChangesAsync();
            }

            return Ok(new { Message = "图片添加成功" });
        }


        // 获取方案内图片列表
        [HttpGet]
        public async Task<IActionResult> GetProjectImages(int projectId)
        {
            var userId = GetCurrentUserId();
            if (!await ValidateProjectAccess(projectId, userId))
                return Forbid();

            try
            {
                _logger.LogInformation("开始查询项目 {ProjectId} 下的图片信息...", projectId);

                // 查询 ImageID 管理的图片
                var images = await _context.projectimages
                    .Where(pi => pi.ProjectID == projectId)
                    .Where(pi => pi.ImageID.HasValue && pi.AiImageID == null && pi.Image != null)
                    .Include(pi => pi.Image)
                    .Include(pi => pi.Room)
                    .Select(pi => new ProjectImageDto
                    {
                        ImageID = pi.Image!.ImageID,
                        AiImageID = null,
                        FileName = pi.Image!.FileName,
                        ThumbnailUrl = pi.Image.ThumbnailPath,
                        FullImageUrl = pi.Image.FilePath,
                        Room = pi.Room != null ? pi.Room.Type ?? "unclassified" : "unclassified",
                        UploadTime = pi.AddedTime,
                        Tags = pi.Image.Tags,
                        RelationID = pi.RelationID
                    })
                    .ToListAsync();

                _logger.LogInformation("查询到 {ImageCount} 张通过 ImageID 管理的图片", images.Count);

                // 查询 AI 图
                var aiImages = await _context.projectimages
                    .Where(pi => pi.ProjectID == projectId
                        && pi.AiImageID != null
                        && pi.AiGenerationJobImage != null)
                    .Include(pi => pi.AiGenerationJobImage)
                    .Include(pi => pi.Room)
                    .Select(pi => new ProjectImageDto
                    {
                        ImageID = null,
                        AiImageID = pi.AiGenerationJobImage!.AiImageID,
                        FileName = pi.AiGenerationJobImage!.ImageUrl ?? string.Empty,
                        ThumbnailUrl = pi.AiGenerationJobImage.ThumbnailPath ?? string.Empty,
                        FullImageUrl = pi.AiGenerationJobImage.ImageUrl ?? string.Empty,
                        Room = pi.Room != null ? pi.Room.Type ?? "unclassified" : "unclassified",
                        UploadTime = pi.AddedTime,
                        Tags = null,
                        RelationID = pi.RelationID
                    })
                    .ToListAsync();

                _logger.LogInformation("查询到 {AiImageCount} 张通过 AiImageID 管理的图片", aiImages.Count);

                var allImages = images.Concat(aiImages).ToList();

                foreach (var image in allImages)
                {
                    image.SourceType = image.AiImageID.HasValue ? "ai" : "gallery";
                    image.ThumbnailUrl = Url.Action(
                        nameof(GetProjectImageFile),
                        new { projectId, relationId = image.RelationID, type = "thumbnail" }) ?? string.Empty;
                    image.FullImageUrl = Url.Action(
                        nameof(GetProjectImageFile),
                        new { projectId, relationId = image.RelationID, type = "original" }) ?? string.Empty;
                }

                _logger.LogInformation("最终合并查询结果，共获取到 {TotalImagesCount} 张图片", allImages.Count);

                return Ok(allImages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取项目图片失败！");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "发生未处理的系统错误",
                    RequestId = HttpContext.TraceIdentifier
                });
            }
        }


        // 移除图片关联
        [HttpDelete("{relationId}")]
        public async Task<IActionResult> RemoveImageFromProject(int projectId, int relationId)
        {
            var userId = GetCurrentUserId();
            if (!await ValidateProjectAccess(projectId, userId))
                return Forbid();

            var relation = await _context.projectimages
                .FirstOrDefaultAsync(pi =>
                    pi.RelationID == relationId &&
                    pi.ProjectID == projectId);
            if (relation == null) return NotFound();

            var aiImageId = relation.AiImageID;
            _context.projectimages.Remove(relation);
            await _context.SaveChangesAsync();

            if (aiImageId.HasValue)
            {
                var isStillAdded = await _context.projectimages
                    .AnyAsync(item => item.AiImageID == aiImageId.Value);
                var aiImage = await _context.aigenerationjobimages
                    .FirstOrDefaultAsync(i => i.AiImageID == aiImageId.Value);
                if (aiImage != null)
                {
                    aiImage.IsAddedToProject = isStillAdded;
                    if (isStillAdded)
                    {
                        aiImage.RetentionStatus = AiGenerationJobImage.RetentionRetained;
                        aiImage.CleanupEligibleAt = null;
                    }
                    else if (aiImage.JobId == null)
                    {
                        aiImage.RetentionStatus = AiGenerationJobImage.RetentionCleanupPending;
                        aiImage.CleanupEligibleAt = DateTime.UtcNow.AddDays(7);
                    }
                    else
                    {
                        aiImage.RetentionStatus = AiGenerationJobImage.RetentionActive;
                        aiImage.CleanupEligibleAt = null;
                    }
                    await _context.SaveChangesAsync();
                }
            }

            return NoContent();
        }

        // 统一读取方案内图片。后端根据关联来源选择普通 COS 或 AI COS。
        [HttpGet("{relationId}/file")]
        public async Task<IActionResult> GetProjectImageFile(
            int projectId,
            int relationId,
            [FromQuery] string type = "original")
        {
            var userId = GetCurrentUserId();
            if (!await ValidateProjectAccess(projectId, userId))
                return Forbid();

            var relation = await _context.projectimages
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.ProjectID == projectId && item.RelationID == relationId);
            if (relation == null)
                return NotFound();

            var normalizedType = type.Equals("thumbnail", StringComparison.OrdinalIgnoreCase)
                ? "thumbnail"
                : "original";

            (Stream Stream, string ContentType) file;
            if (relation.AiImageID.HasValue)
            {
                file = await _context.aigenerationjobimages
                    .Where(image => image.AiImageID == relation.AiImageID.Value)
                    .AnyAsync()
                    ? await _cosService.GetImageStreamByAiImageIdAsync(
                        relation.AiImageID.Value,
                        normalizedType)
                    : throw new KeyNotFoundException("AI图片不存在或无权访问");
            }
            else if (relation.ImageID.HasValue)
            {
                file = await _cosService.GetImageStreamAsync(
                    relation.ImageID.Value,
                    normalizedType == "thumbnail" ? "thumbnail" : null);
            }
            else
            {
                return NotFound();
            }

            return File(file.Stream, file.ContentType, enableRangeProcessing: true);
        }

        [HttpGet("{relationId}", Name = "GetProjectImage")] // 添加 Name 属性
        public async Task<IActionResult> GetImage(int projectId, int relationId)
        {
            var userId = GetCurrentUserId();
            if (!await ValidateProjectAccess(projectId, userId))
                return Forbid();

            var relation = await _context.projectimages
                .Include(pi => pi.Image)
                .Include(pi => pi.Room)
                .FirstOrDefaultAsync(pi =>
                    pi.RelationID == relationId &&
                    pi.ProjectID == projectId);

            return relation != null ? Ok(relation) : NotFound();
        }

        private async Task<bool> ValidateProjectAccess(int projectId, int userId)
        {
            return await _context.projects
                .AnyAsync(p => p.ProjectID == projectId && p.UserID == userId);
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                throw new InvalidOperationException("User ID claim not found");
            }
            return int.Parse(userIdClaim);
        }
    }
    public class ProjectImageDto
    {
        public int? ImageID { get; set; }
        public int? AiImageID { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
        public string FullImageUrl { get; set; } = string.Empty;
        public string Room { get; set; } = "unclassified";
        public string SourceType { get; set; } = "gallery";
        public DateTime UploadTime { get; set; }
        public string? Tags { get; set; }
        public int RelationID { get; set; }
    }
}
