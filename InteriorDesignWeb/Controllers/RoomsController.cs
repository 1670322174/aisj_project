using InteriorDesignWeb.Data;
using InteriorDesignWeb.Models.Entities;
using InteriorDesignWeb.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace InteriorDesignWeb.Controllers
{
    [ApiController]
    [Route("api/projects/{projectId}/[controller]")]
    [Authorize]
    public class RoomsController : ControllerBase
    {
        private readonly DesignHubContext _context;
        private readonly ILogger<RoomsController> _logger;

        public RoomsController(
            DesignHubContext context,
            ILogger<RoomsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // 获取方案所有房间（树形结构）
        [HttpGet]
        public async Task<IActionResult> GetRooms(int projectId)
        {
            var userId = GetCurrentUserId();
            if (!await ValidateProjectAccess(projectId, userId))
                return Forbid();

            var rooms = await _context.projectrooms
                .Where(r => r.ProjectID == projectId)
                .OrderBy(r => r.OrderIndex)
                .ToListAsync();

            // 构建树形结构
            var rootRooms = rooms.Where(r => r.ParentRoomID == null).ToList();
            foreach (var room in rootRooms)
            {
                room.Children = GetChildren(rooms, room.RoomID);
            }

            return Ok(rootRooms);
        }

        [Authorize(Roles = "FreeUser, Administrator, Member, PremiumMember")]
        [HttpPost]
        public async Task<ActionResult<RoomDto>> AddRoom(int projectId, [FromBody] RoomCreateRequest request)
        {
            var userId = GetCurrentUserId();

            // 验证项目权限
            var project = await _context.projects
                .FirstOrDefaultAsync(p => p.ProjectID == projectId && p.UserID == userId);
            if (project == null) return Forbid();

            // 验证房间类型
            var validTypes = new[] { "bedroom", "living_room", "bathroom", "balcony" };
            if (!validTypes.Contains(request.Type?.ToLower()))
                return BadRequest("无效的房间类型");

            // 生成排序索引
            var maxOrder = await _context.projectrooms
                .Where(r => r.ProjectID == projectId)
                .MaxAsync(r => (int?)r.OrderIndex) ?? 0;

            var room = new ProjectRoom
            {
                ProjectID = projectId,
                Name = request.Name,
                OrderIndex = maxOrder + 1,
                Type = request.Type?.ToLower()
            };

            await _context.projectrooms.AddAsync(room);
            await _context.SaveChangesAsync();

            //return CreatedAtAction(nameof(GetRoom), new { projectId, roomId = room.RoomID }, room);
            return CreatedAtAction(nameof(GetRoom), new { projectId, roomId = room.RoomID },
        new RoomDto
        {
            RoomID = room.RoomID,
            Name = room.Name,
            Type = room.Type,
            ParentRoomID = room.ParentRoomID
        });
        }

        // 更新房间信息
        [HttpPut("{roomId}")]
        public async Task<IActionResult> UpdateRoom(int projectId, int roomId, [FromBody] RoomUpdateRequest request)
        {
            var userId = GetCurrentUserId();
            if (!await ValidateProjectAccess(projectId, userId))
                return Forbid();

            var room = await _context.projectrooms
                .FirstOrDefaultAsync(r => r.RoomID == roomId && r.ProjectID == projectId);

            if (room == null) return NotFound();

            room.Name = request.Name ?? room.Name;
            room.OrderIndex = request.OrderIndex ?? room.OrderIndex;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // 删除房间（级联删除子房间）
        [HttpDelete("{roomId}")]
        public async Task<IActionResult> DeleteRoom(int projectId, int roomId)
        {
            var userId = GetCurrentUserId();
            if (!await ValidateProjectAccess(projectId, userId))
                return Forbid();

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var room = await _context.projectrooms
                    .Include(r => r.Children)
                    .FirstOrDefaultAsync(r => r.RoomID == roomId && r.ProjectID == projectId);

                if (room == null) return NotFound();

                await DeleteRoomRecursive(room);
                await transaction.CommitAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "删除房间失败");
                return StatusCode(500);
            }
        }

        // 在 RoomsController 中添加
        [HttpGet("{roomId}")]
        public async Task<IActionResult> GetRoom(int projectId, int roomId)
        {
            var userId = GetCurrentUserId();
            if (!await ValidateProjectAccess(projectId, userId))
                return Forbid();

            var room = await _context.projectrooms
                .Include(r => r.Children)
                .Include(r => r.Images)
                .FirstOrDefaultAsync(r =>
                    r.RoomID == roomId &&
                    r.ProjectID == projectId);

            return room != null ? Ok(room) : NotFound();
        }

        private async Task DeleteRoomRecursive(ProjectRoom room)
        {
            foreach (var child in room.Children.ToList())
            {
                await DeleteRoomRecursive(child);
            }
            _context.projectrooms.Remove(room);
            await _context.SaveChangesAsync();
        }

        private List<ProjectRoom> GetChildren(List<ProjectRoom> allRooms, int parentId)
        {
            return allRooms
                .Where(r => r.ParentRoomID == parentId)
                .OrderBy(r => r.OrderIndex)
                .ToList();
        }

        private async Task<bool> ValidateProjectAccess(int projectId, int userId)
        {
            return await _context.projects
                .AnyAsync(p => p.ProjectID == projectId && p.UserID == userId);
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim))
            {
                throw new InvalidOperationException("当前用户的标识符不存在。");
            }
            return int.Parse(userIdClaim);
        }
    }
    // 返回用DTO
    public class RoomDto
    {
        public int RoomID { get; set; }
        public string? Name { get; set; }
        public string? Type { get; set; }
        public int? ParentRoomID { get; set; }
    }
}
