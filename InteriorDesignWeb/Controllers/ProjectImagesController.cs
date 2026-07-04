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

        public ProjectImagesController(
            DesignHubContext context,
            ILogger<ProjectImagesController> logger,
            IQuotaService quotaService,
            IRoleLimitService roleLimitService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _quotaService = quotaService ?? throw new ArgumentNullException(nameof(quotaService));
            _roleLimitService = roleLimitService ?? throw new ArgumentNullException(nameof(roleLimitService));
        }

        // 添加图片到方案房间
        [HttpPost]
        public async Task<IActionResult> AddImageToProject(int projectId, [FromBody] ProjectImageAddRequest request)
        {
            var userId = GetCurrentUserId();
            // 验证项目权限
            if (!await ValidateProjectAccess(projectId, userId))
                return Forbid();

            // 如果是通过 ImageID 添加
            if (request.ImageID != 0)
            {
                // 如果 RoomID 为 0 或 null，不进行验证
                if (request.RoomID != null && request.RoomID != 0)
                {
                    var roomExists = await _context.projectrooms
                        .AnyAsync(r => r.RoomID == request.RoomID && r.ProjectID == projectId);

                    if (!roomExists)
                        return BadRequest("房间不存在");
                }

                var imageExists = await _context.images
                    .AnyAsync(i => i.ImageID == request.ImageID);

                if (!imageExists)
                    return BadRequest("图片不存在");

                var relation = new ProjectImage
                {
                    ProjectID = projectId,
                    RoomID = request.RoomID == 0 ? null : request.RoomID,  // 如果为0，则设置为null
                    ImageID = request.ImageID,
                    CustomTags = request.CustomTags
                };

                await _context.projectimages.AddAsync(relation);
                await _context.SaveChangesAsync();
            }
            // 如果是通过 AiImageID 添加
            else if (request.AiImageID != 0)
            {
                var aiImageExists = await _context.aigenerationjobimages
                    .AnyAsync(i => i.AiImageID == request.AiImageID);

                if (!aiImageExists)
                    return BadRequest("AI生成的图片不存在");

                var relation = new ProjectImage
                {
                    ProjectID = projectId,
                    RoomID = null,  // 当通过 AiImageID 添加时，RoomID 为空
                    AiImageID = request.AiImageID,  // 通过 AiImageID 关联图片
                    CustomTags = request.CustomTags
                };

                // 将 AiGenerationJobImage 中的 IsAddedToProject 更新为 True
                var aiImage = await _context.aigenerationjobimages
                    .FirstOrDefaultAsync(i => i.AiImageID == request.AiImageID);
                if (aiImage != null)
                {
                    aiImage.IsAddedToProject = true;
                    _context.Update(aiImage);
                }

                // 不插入 ImageID，只插入 AiImageID
                await _context.projectimages.AddAsync(relation);
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
                    .Where(pi => pi.ImageID != 0 && pi.AiImageID == null)
                    .Include(pi => pi.Image)
                    .Include(pi => pi.Room)
                    .Select(pi => new ProjectImageDto
                    {
                        ImageID = pi.Image.ImageID,
                        AiImageID = null,
                        FileName = pi.Image.FileName,
                        ThumbnailUrl = pi.Image.ThumbnailPath,
                        FullImageUrl = pi.Image.FilePath,
                        Room = pi.Room != null ? pi.Room.Type : "unclassified", // ✅ 避免 null 报错
                        UploadTime = pi.AddedTime,
                        Tags = pi.Image.Tags,
                        RelationID = pi.RelationID
                    })
                    .ToListAsync();

                _logger.LogInformation("查询到 {ImageCount} 张通过 ImageID 管理的图片", images.Count);

                // 查询 AI 图
                var aiImages = await _context.projectimages
                    .Where(pi => pi.ProjectID == projectId && pi.AiImageID != null)
                    .Include(pi => pi.AiGenerationJobImage)
                    .Select(pi => new ProjectImageDto
                    {
                        ImageID = null,
                        AiImageID = pi.AiGenerationJobImage!.AiImageID,
                        FileName = pi.AiGenerationJobImage.ImageUrl,
                        ThumbnailUrl = pi.AiGenerationJobImage.ThumbnailPath,
                        FullImageUrl = pi.AiGenerationJobImage.ImageUrl,
                        Room = "null",
                        UploadTime = pi.AddedTime,
                        Tags = (string)null,
                        RelationID = pi.RelationID
                    })
                    .ToListAsync();

                _logger.LogInformation("查询到 {AiImageCount} 张通过 AiImageID 管理的图片", aiImages.Count);

                var allImages = images.Concat(aiImages).ToList();

                _logger.LogInformation("最终合并查询结果，共获取到 {TotalImagesCount} 张图片", allImages.Count);

                return Ok(allImages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取项目图片失败！");
                return StatusCode(500, new { Success = false, Message = "发生未处理的系统错误", ExceptionMessage = ex.Message });
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
            if (relation.AiImageID.HasValue)
            {
                var aiImage = await _context.aigenerationjobimages
                    .FirstOrDefaultAsync(i => i.AiImageID == relation.AiImageID.Value);
                if (aiImage != null)
                {
                    aiImage.IsAddedToProject = false;
                    _context.Update(aiImage);
                }
            }
            if (relation == null) return NotFound();

            _context.projectimages.Remove(relation);
            await _context.SaveChangesAsync();

            return NoContent();
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
        public string FileName { get; set; }
        public string ThumbnailUrl { get; set; }
        public string FullImageUrl { get; set; }
        public string Room { get; set; }
        public DateTime UploadTime { get; set; }
        public string Tags { get; set; }
        public int RelationID { get; set; }
    }
}
