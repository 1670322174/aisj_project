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


namespace InteriorDesignWeb.Controllers
{
    /*4/6*/
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProjectsController : ControllerBase
    {
        private readonly DesignHubContext _context;
        private readonly ILogger<ProjectsController> _logger;
        private readonly IQuotaService _quotaService;  // 通过接口注入

        // Replace the incorrect using directive with the correct one.  
        // The error indicates that "QuotaService" is a type, not a namespace.  
        // To fix this, remove the incorrect "using" directive and directly use the type where needed.  

        // Remove this line:  
        // using InteriorDesignWeb.Services.QuotaService;  

        // Add a private field for QuotaService in the controller and inject it via the constructor.  

        private readonly IRoleLimitService _roleLimitService;

        public ProjectsController(
            DesignHubContext context,
            ILogger<ProjectsController> logger,
            IQuotaService quotaService,

            IRoleLimitService roleLimitService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _quotaService = quotaService ?? throw new ArgumentNullException(nameof(quotaService));
            _roleLimitService = roleLimitService ?? throw new ArgumentNullException(nameof(roleLimitService));
        }

        // 获取用户所有方案
        [HttpGet]
        public async Task<IActionResult> GetProjects()
        {
            var userId = GetCurrentUserId();
            var projects = await _context.projects
                .Where(p => p.UserID == userId)
                .AsSplitQuery()  // 添加拆分查询
                .Select(p => new
                {
                    p.ProjectID,
                    p.Name,
                    p.Description,
                    p.CreatedAt,
                    RoomCount = _context.projectrooms.Count(r => r.ProjectID == p.ProjectID),
                    ImageCount = _context.projectimages.Count(i => i.ProjectID == p.ProjectID)
                })
                .ToListAsync();

            return Ok(projects);
        }

        // Update the code to ensure the `user` variable is not null before passing it to `_userManager.GetRolesAsync`.

        [Authorize(Roles = "FreeUser, Administrator, Member, PremiumMember")]
        [HttpPost]
        public async Task<IActionResult> CreateProject([FromBody] ProjectCreateRequest request)
        {
            var userId = GetCurrentUserId();

            // 检查配额
            if (!await _quotaService.CanCreateProject(userId))
            {
                // 使用DbContext直接查询用户
                var user = await _context.users.FindAsync(userId);
                if (user == null)
                {
                    return NotFound(new
                    {
                        Code = "USER_NOT_FOUND",
                        Message = "用户未找到。"
                    });
                }

                // 直接获取用户角色（单个角色）
                var limit = _roleLimitService.GetSchemeCreationLimit(user.Role);

                return Conflict(new
                {
                    Code = "QUOTA_EXCEEDED",
                    Message = $"已达到最大方案数量限制（{limit}个）"
                });
            }

            var project = new Project
            {
                UserID = userId,
                Name = request.Name!,
                Description = request.Description,
                IsTemplate = request.IsTemplate ?? false,
                CreatedAt = DateTime.UtcNow
            };

            await _context.projects.AddAsync(project);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetProject), new { id = project.ProjectID }, project);
        }

        // 获取方案详情
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProject(int id)
        {
            var userId = GetCurrentUserId();
            var project = await _context.projects
                .Include(p => p.Rooms)
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.ProjectID == id && p.UserID == userId);

            return project != null ? Ok(project) : NotFound();
        }

        // 更新方案信息
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProject(int id, [FromBody] ProjectUpdateRequest request)
        {
            var userId = GetCurrentUserId();
            var project = await _context.projects
                .FirstOrDefaultAsync(p => p.ProjectID == id && p.UserID == userId);

            if (project == null) return NotFound();

            project.Name = request.Name ?? project.Name;
            project.Description = request.Description ?? project.Description;
            project.IsTemplate = request.IsTemplate;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // 删除方案
        // 修改删除接口
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProject(int id)
        {
            var userId = GetCurrentUserId();

            // 包含关联数据
            var project = await _context.projects
                .Include(p => p.Images) // 加载关联图片
                .FirstOrDefaultAsync(p => p.ProjectID == id && p.UserID == userId);

            if (project == null) return NotFound();

            // 删除所有关联图片（可选）
            _context.projectimages.RemoveRange(project.Images);

            // 删除主实体
            _context.projects.Remove(project);

            await _context.SaveChangesAsync();

            return NoContent();
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim))
            {
                throw new InvalidOperationException("User ID claim is missing or invalid.");
            }
            return int.Parse(userIdClaim);
        }
    }
}
