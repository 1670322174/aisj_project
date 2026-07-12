using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Text.Json;
using InteriorDesignWeb.Data;
using InteriorDesignWeb.Models.DTOs;
using InteriorDesignWeb.Models.DTOs.Admin;
using InteriorDesignWeb.Models.Entities;
using InteriorDesignWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;

namespace InteriorDesignWeb.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = nameof(UserRole.Administrator))]
public sealed class AdminController : ControllerBase
{
    private const long MaxGalleryUploadBytes = 20L * 1024 * 1024;
    private const long MaxDecodedPixels = 50_000_000;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private static readonly HashSet<string> AllowedRoomTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "living_room", "bedroom", "dining_room", "kitchen", "bathroom", "study", "balcony", "other",
        "客厅", "卧室", "餐厅", "厨房", "卫生间", "书房", "阳台", "其他"
    };

    private static readonly string[] SuccessfulJobStatuses = { "completed", "succeeded", "success" };
    private static readonly string[] FailedJobStatuses = { "failed", "error", "cancelled", "canceled", "timeout" };
    private static readonly string[] RunningJobStatuses = { "created", "queued", "running", "processing", "uploading" };

    private readonly DesignHubContext _context;
    private readonly CosService _cosService;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly IActionDescriptorCollectionProvider _actionDescriptors;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        DesignHubContext context,
        CosService cosService,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        IActionDescriptorCollectionProvider actionDescriptors,
        ILogger<AdminController> logger)
    {
        _context = context;
        _cosService = cosService;
        _configuration = configuration;
        _environment = environment;
        _actionDescriptors = actionDescriptors;
        _logger = logger;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview([FromQuery] int days = 14, CancellationToken cancellationToken = default)
    {
        days = Math.Clamp(days, 7, 90);
        var now = DateTime.UtcNow;
        var today = now.Date;
        var trendStart = today.AddDays(-(days - 1));

        var totalUsers = await _context.users.AsNoTracking().CountAsync(cancellationToken);
        var enabledUsers = await _context.users.AsNoTracking().CountAsync(user => user.IsEnabled, cancellationToken);
        var newUsersToday = await _context.users.AsNoTracking().CountAsync(user => user.RegisterTime >= today, cancellationToken);
        var activeUsers24h = await _context.users.AsNoTracking()
            .CountAsync(user => user.LastActivityAt >= now.AddHours(-24), cancellationToken);

        var totalProjects = await _context.projects.AsNoTracking().CountAsync(project => !project.IsDeleted, cancellationToken);
        var newProjectsToday = await _context.projects.AsNoTracking()
            .CountAsync(project => !project.IsDeleted && project.CreatedAt >= today, cancellationToken);

        var galleryCount = await _context.images.AsNoTracking().CountAsync(cancellationToken);
        var newGalleryToday = await _context.images.AsNoTracking()
            .CountAsync(image => image.UploadTime >= today, cancellationToken);
        var galleryBytes = await _context.images.AsNoTracking()
            .Select(image => (long?)image.FileSize)
            .SumAsync(cancellationToken) ?? 0;

        var aiImageCount = await _context.aigenerationjobimages.AsNoTracking().CountAsync(cancellationToken);
        var totalJobs = await _context.aigenerationjobs.AsNoTracking().CountAsync(job => !job.IsDeleted, cancellationToken);
        var jobsToday = await _context.aigenerationjobs.AsNoTracking()
            .CountAsync(job => !job.IsDeleted && job.CreatedAt >= today, cancellationToken);
        var succeededJobs = await _context.aigenerationjobs.AsNoTracking()
            .CountAsync(job => !job.IsDeleted && SuccessfulJobStatuses.Contains(job.Status), cancellationToken);
        var failedJobs = await _context.aigenerationjobs.AsNoTracking()
            .CountAsync(job => !job.IsDeleted && FailedJobStatuses.Contains(job.Status), cancellationToken);
        var runningJobs = await _context.aigenerationjobs.AsNoTracking()
            .CountAsync(job => !job.IsDeleted && RunningJobStatuses.Contains(job.Status), cancellationToken);

        var statusDistribution = await _context.aigenerationjobs.AsNoTracking()
            .Where(job => !job.IsDeleted)
            .GroupBy(job => job.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .OrderByDescending(item => item.Count)
            .ToListAsync(cancellationToken);

        var retentionDistribution = await _context.aigenerationjobimages.AsNoTracking()
            .GroupBy(image => image.RetentionStatus)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .OrderByDescending(item => item.Count)
            .ToListAsync(cancellationToken);

        var userDates = await _context.users.AsNoTracking()
            .Where(user => user.RegisterTime >= trendStart)
            .Select(user => user.RegisterTime)
            .ToListAsync(cancellationToken);
        var jobTrendRows = await _context.aigenerationjobs.AsNoTracking()
            .Where(job => !job.IsDeleted && job.CreatedAt >= trendStart)
            .Select(job => new { job.CreatedAt, job.Status })
            .ToListAsync(cancellationToken);

        var usersByDay = userDates.GroupBy(value => value.Date).ToDictionary(group => group.Key, group => group.Count());
        var jobsByDay = jobTrendRows.GroupBy(value => value.CreatedAt.Date).ToDictionary(group => group.Key, group => group.ToList());
        var trends = Enumerable.Range(0, days).Select(offset =>
        {
            var date = trendStart.AddDays(offset);
            jobsByDay.TryGetValue(date, out var rows);
            return new
            {
                Date = date.ToString("yyyy-MM-dd"),
                Users = usersByDay.GetValueOrDefault(date),
                Jobs = rows?.Count ?? 0,
                Succeeded = rows?.Count(row => SuccessfulJobStatuses.Contains(row.Status)) ?? 0,
                Failed = rows?.Count(row => FailedJobStatuses.Contains(row.Status)) ?? 0
            };
        }).ToList();

        var recentFailureRows = await _context.aigenerationjobs.AsNoTracking()
            .Where(job => !job.IsDeleted && FailedJobStatuses.Contains(job.Status))
            .OrderByDescending(job => job.UpdatedAt ?? job.CreatedAt)
            .Select(job => new
            {
                job.JobId,
                job.UserID,
                Username = job.User != null ? job.User.UserName : null,
                job.WorkflowCode,
                job.Prompt,
                job.Status,
                job.CreatedAt,
                job.CompletedAt,
                job.ErrorMessage
            })
            .Take(10)
            .ToListAsync(cancellationToken);

        var recentFailedJobs = recentFailureRows.Select(job => new
        {
            job.JobId,
            UserId = job.UserID,
            job.Username,
            job.WorkflowCode,
            Prompt = Truncate(job.Prompt, 160),
            job.Status,
            job.CreatedAt,
            job.CompletedAt,
            ErrorMessage = Truncate(job.ErrorMessage, 300)
        });

        var terminalJobs = succeededJobs + failedJobs;
        var successRate = terminalJobs == 0 ? 0 : Math.Round(succeededJobs * 100d / terminalJobs, 1);
        var activeSessions = await _context.usersessions.AsNoTracking()
            .CountAsync(session => session.RevokedAt == null && session.ExpiresAt > now, cancellationToken);

        return OkEnvelope(new
        {
            Users = new
            {
                Total = totalUsers,
                Enabled = enabledUsers,
                Disabled = totalUsers - enabledUsers,
                NewToday = newUsersToday,
                Active24h = activeUsers24h,
                ActiveSessions = activeSessions
            },
            Projects = new { Total = totalProjects, NewToday = newProjectsToday },
            Gallery = new { Total = galleryCount, NewToday = newGalleryToday },
            AiJobs = new
            {
                Total = totalJobs,
                Today = jobsToday,
                Running = runningJobs,
                Succeeded = succeededJobs,
                Failed = failedJobs,
                SuccessRate = successRate,
                StatusDistribution = statusDistribution
            },
            Storage = new
            {
                Objects = galleryCount + aiImageCount,
                Bytes = galleryBytes,
                DisplaySize = FormatBytes(galleryBytes),
                GalleryObjects = galleryCount,
                AiObjects = aiImageCount,
                AiRetentionDistribution = retentionDistribution,
                BytesNote = "仅普通图库记录包含可汇总的文件大小"
            },
            Trends = trends,
            RecentFailedJobs = recentFailedJobs
        });
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? role = null,
        [FromQuery] bool? isEnabled = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _context.users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            query = query.Where(user => user.UserName.Contains(keyword) || user.PhoneNumber.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            if (!TryParseRole(role, out var parsedRole))
                return BadRequestEnvelope("不支持的用户角色");
            query = query.Where(user => user.Role == parsedRole);
        }

        if (isEnabled.HasValue)
            query = query.Where(user => user.IsEnabled == isEnabled.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(user => user.RegisterTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(user => new
            {
                UserId = user.UserID,
                Username = user.UserName,
                Phone = user.PhoneNumber,
                Role = user.Role.ToString(),
                user.IsEnabled,
                CreatedAt = user.RegisterTime,
                user.LastLoginAt,
                user.LastActivityAt,
                ProjectCount = _context.projects.Count(project => project.UserID == user.UserID && !project.IsDeleted),
                JobCount = _context.aigenerationjobs.Count(job => job.UserID == user.UserID && !job.IsDeleted),
                SessionCount = _context.usersessions.Count(session => session.UserID == user.UserID && session.RevokedAt == null && session.ExpiresAt > DateTime.UtcNow)
            })
            .ToListAsync(cancellationToken);

        return OkEnvelope(new
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] AdminCreateUserRequest request, CancellationToken cancellationToken)
    {
        if (!TryParseRole(request.Role, out var role))
            return BadRequestEnvelope("不支持的用户角色");

        var username = request.Username.Trim();
        var phone = request.PhoneNumber?.Trim() ?? string.Empty;
        if (await _context.users.AnyAsync(user => user.UserName == username, cancellationToken))
            return ConflictEnvelope("用户名已存在");
        if (phone.Length > 0 && await _context.users.AnyAsync(user => user.PhoneNumber == phone, cancellationToken))
            return ConflictEnvelope("手机号已存在");

        var now = DateTime.UtcNow;
        var user = new User
        {
            UserName = username,
            PhoneNumber = phone,
            Role = role,
            RegisterTime = now,
            UpdatedAt = now,
            IsEnabled = true,
            AuthVersion = 1
        };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, request.Password);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        _context.users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);
        AddAudit("user.create", "user", user.UserID.ToString(), $"创建用户 {user.UserName}", new { Role = role.ToString() });
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return OkEnvelope(new
        {
            UserId = user.UserID,
            Username = user.UserName,
            Phone = user.PhoneNumber,
            Role = user.Role.ToString(),
            user.IsEnabled,
            CreatedAt = user.RegisterTime,
            ProjectCount = 0,
            JobCount = 0,
            SessionCount = 0
        }, "用户创建成功");
    }

    [HttpGet("users/{userId:int}")]
    public async Task<IActionResult> GetUserDetail(int userId, CancellationToken cancellationToken)
    {
        var user = await _context.users.AsNoTracking().FirstOrDefaultAsync(item => item.UserID == userId, cancellationToken);
        if (user == null) return NotFoundEnvelope("用户不存在");

        var now = DateTime.UtcNow;
        var projectCount = await _context.projects.AsNoTracking().CountAsync(project => project.UserID == userId && !project.IsDeleted, cancellationToken);
        var jobCount = await _context.aigenerationjobs.AsNoTracking().CountAsync(job => job.UserID == userId && !job.IsDeleted, cancellationToken);
        var imageCount = await _context.aigenerationjobimages.AsNoTracking().CountAsync(image => image.UserID == userId, cancellationToken);
        var activeSessionCount = await _context.usersessions.AsNoTracking()
            .CountAsync(session => session.UserID == userId && session.RevokedAt == null && session.ExpiresAt > now, cancellationToken);

        var quota = await _context.userquotas.AsNoTracking()
            .Where(item => item.UserID == userId)
            .Select(item => new { Total = item.TotalUnits, Used = item.UsedUnits, Available = item.RemainingUnits })
            .FirstOrDefaultAsync(cancellationToken);

        var recentProjects = await _context.projects.AsNoTracking()
            .Where(project => project.UserID == userId && !project.IsDeleted)
            .OrderByDescending(project => project.UpdatedAt ?? project.CreatedAt)
            .Take(10)
            .Select(project => new
            {
                ProjectId = project.ProjectID,
                project.Name,
                project.CreatedAt,
                ImageCount = project.Images.Count
            })
            .ToListAsync(cancellationToken);

        var recentJobs = await _context.aigenerationjobs.AsNoTracking()
            .Where(job => job.UserID == userId && !job.IsDeleted)
            .OrderByDescending(job => job.CreatedAt)
            .Take(10)
            .Select(job => new
            {
                job.JobId,
                UserId = job.UserID,
                Username = user.UserName,
                job.WorkflowCode,
                job.Prompt,
                job.Status,
                job.CreatedAt,
                job.CompletedAt,
                job.ErrorMessage
            })
            .ToListAsync(cancellationToken);

        var recentActivities = await _context.projectactivities.AsNoTracking()
            .Where(activity => activity.UserID == userId)
            .OrderByDescending(activity => activity.CreatedAt)
            .Take(20)
            .Select(activity => new
            {
                Id = activity.ActivityID,
                Action = activity.ActivityType,
                Description = activity.Title ?? activity.Content,
                activity.CreatedAt,
                IpAddress = (string?)null,
                Result = "Success"
            })
            .ToListAsync(cancellationToken);

        var sessions = await _context.usersessions.AsNoTracking()
            .Where(session => session.UserID == userId)
            .OrderByDescending(session => session.CreatedAt)
            .Take(50)
            .Select(session => new
            {
                SessionId = session.UserSessionID,
                session.CreatedAt,
                session.LastUsedAt,
                session.ExpiresAt,
                session.IpAddress,
                session.UserAgent,
                IsCurrent = false,
                IsRevoked = session.RevokedAt != null,
                session.RevokedAt
            })
            .ToListAsync(cancellationToken);

        return OkEnvelope(new
        {
            Summary = new
            {
                UserId = user.UserID,
                Username = user.UserName,
                Phone = user.PhoneNumber,
                Role = user.Role.ToString(),
                user.IsEnabled,
                CreatedAt = user.RegisterTime,
                user.LastLoginAt,
                user.LastActivityAt,
                ProjectCount = projectCount,
                JobCount = jobCount,
                SessionCount = activeSessionCount,
                user.DisabledAt
            },
            Quota = quota ?? new { Total = 0, Used = 0, Available = 0 },
            Counts = new { Projects = projectCount, Jobs = jobCount, Images = imageCount, Sessions = activeSessionCount },
            RecentProjects = recentProjects,
            RecentJobs = recentJobs.Select(job => new
            {
                job.JobId,
                job.UserId,
                job.Username,
                job.WorkflowCode,
                Prompt = Truncate(job.Prompt, 300),
                job.Status,
                job.CreatedAt,
                job.CompletedAt,
                ErrorMessage = Truncate(job.ErrorMessage, 300)
            }),
            RecentActivities = recentActivities,
            Sessions = sessions
        });
    }

    [HttpPut("users/{userId:int}/role")]
    public async Task<IActionResult> ChangeUserRole(int userId, [FromBody] AdminChangeRoleRequest request, CancellationToken cancellationToken)
    {
        if (!TryParseRole(request.Role, out var newRole))
            return BadRequestEnvelope("不支持的用户角色");

        var user = await _context.users.FirstOrDefaultAsync(item => item.UserID == userId, cancellationToken);
        if (user == null) return NotFoundEnvelope("用户不存在");
        if (user.Role == newRole) return OkEnvelope(new { user.UserID, Role = user.Role.ToString() }, "角色未发生变化");

        if (user.IsEnabled && user.Role == UserRole.Administrator && newRole != UserRole.Administrator
            && await CountEnabledAdministrators(cancellationToken) <= 1)
        {
            return ConflictEnvelope("不能移除系统中最后一个启用管理员");
        }

        var oldRole = user.Role;
        user.Role = newRole;
        user.AuthVersion++;
        user.UpdatedAt = DateTime.UtcNow;
        var revokedSessions = await RevokeActiveSessions(userId, cancellationToken);
        AddAudit("user.role.change", "user", userId.ToString(), $"修改用户 {user.UserName} 的角色", new
        {
            PreviousRole = oldRole.ToString(),
            NewRole = newRole.ToString(),
            RevokedSessions = revokedSessions
        });
        await _context.SaveChangesAsync(cancellationToken);

        return OkEnvelope(new { user.UserID, Role = user.Role.ToString(), RevokedSessions = revokedSessions }, "角色已更新");
    }

    [HttpPut("users/{userId:int}/status")]
    public async Task<IActionResult> ChangeUserStatus(int userId, [FromBody] AdminChangeStatusRequest request, CancellationToken cancellationToken)
    {
        var administratorId = GetAdministratorId();
        if (!request.IsEnabled && userId == administratorId)
            return ConflictEnvelope("管理员不能禁用自己的账号");

        var user = await _context.users.FirstOrDefaultAsync(item => item.UserID == userId, cancellationToken);
        if (user == null) return NotFoundEnvelope("用户不存在");
        if (user.IsEnabled == request.IsEnabled)
            return OkEnvelope(new { user.UserID, user.IsEnabled }, "账号状态未发生变化");

        if (!request.IsEnabled && user.Role == UserRole.Administrator
            && await CountEnabledAdministrators(cancellationToken) <= 1)
        {
            return ConflictEnvelope("不能禁用系统中最后一个启用管理员");
        }

        var now = DateTime.UtcNow;
        user.IsEnabled = request.IsEnabled;
        user.DisabledAt = request.IsEnabled ? null : now;
        user.DisabledByUserID = request.IsEnabled ? null : administratorId;
        user.AuthVersion++;
        user.UpdatedAt = now;
        var revokedSessions = await RevokeActiveSessions(userId, cancellationToken);
        AddAudit(
            request.IsEnabled ? "user.enable" : "user.disable",
            "user",
            userId.ToString(),
            request.IsEnabled ? $"启用用户 {user.UserName}" : $"禁用用户 {user.UserName}",
            new { request.IsEnabled, Reason = Truncate(request.Reason, 300), RevokedSessions = revokedSessions });
        await _context.SaveChangesAsync(cancellationToken);

        return OkEnvelope(new { user.UserID, user.IsEnabled, RevokedSessions = revokedSessions }, "账号状态已更新");
    }

    [HttpPut("users/{userId:int}/password")]
    public async Task<IActionResult> ResetUserPassword(int userId, [FromBody] AdminResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await _context.users.FirstOrDefaultAsync(item => item.UserID == userId, cancellationToken);
        if (user == null) return NotFoundEnvelope("用户不存在");

        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, request.NewPassword);
        user.AuthVersion++;
        user.UpdatedAt = DateTime.UtcNow;
        var revokedSessions = await RevokeActiveSessions(userId, cancellationToken);
        AddAudit("user.password.reset", "user", userId.ToString(), $"重置用户 {user.UserName} 的密码", new
        {
            RevokedSessions = revokedSessions
        });
        await _context.SaveChangesAsync(cancellationToken);

        return OkEnvelope(new { user.UserID, RevokedSessions = revokedSessions }, "密码已重置，用户需要重新登录");
    }

    [HttpDelete("users/{userId:int}/sessions/{sessionId:long}")]
    public async Task<IActionResult> RevokeSession(int userId, long sessionId, CancellationToken cancellationToken)
    {
        var session = await _context.usersessions
            .FirstOrDefaultAsync(item => item.UserID == userId && item.UserSessionID == sessionId, cancellationToken);
        if (session == null) return NotFoundEnvelope("会话不存在");

        if (session.RevokedAt == null)
            session.RevokedAt = DateTime.UtcNow;
        AddAudit("user.session.revoke", "user_session", sessionId.ToString(), "撤销用户登录会话", new { UserId = userId });
        await _context.SaveChangesAsync(cancellationToken);
        return OkEnvelope(new { UserId = userId, SessionId = sessionId }, "会话已撤销");
    }

    [HttpDelete("users/{userId:int}/sessions")]
    public async Task<IActionResult> RevokeAllSessions(int userId, CancellationToken cancellationToken)
    {
        if (!await _context.users.AnyAsync(user => user.UserID == userId, cancellationToken))
            return NotFoundEnvelope("用户不存在");

        var revokedSessions = await RevokeActiveSessions(userId, cancellationToken);
        AddAudit("user.sessions.revoke_all", "user", userId.ToString(), "撤销用户全部登录会话", new { RevokedSessions = revokedSessions });
        await _context.SaveChangesAsync(cancellationToken);
        return OkEnvelope(new { UserId = userId, RevokedSessions = revokedSessions }, "登录会话已全部撤销");
    }

    [HttpPost("gallery/images")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxGalleryUploadBytes)]
    public async Task<IActionResult> UploadGalleryImage([FromForm] ImageUploadRequest request, CancellationToken cancellationToken)
    {
        var validationError = ValidateGalleryUpload(request);
        if (validationError != null) return BadRequestEnvelope(validationError);

        var file = request.file!;
        var safeFileName = Path.GetFileName(file.FileName);
        await using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        ImageInfo? imageInfo;
        try
        {
            imageInfo = await SixLabors.ImageSharp.Image.IdentifyAsync(buffer, cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidImageContentException or UnknownImageFormatException)
        {
            return BadRequestEnvelope("文件不是有效的图片");
        }

        if (imageInfo == null || (long)imageInfo.Width * imageInfo.Height > MaxDecodedPixels)
            return BadRequestEnvelope("图片尺寸过大或无法识别");

        try
        {
            buffer.Position = 0;
            var (cosPath, fileHash) = await _cosService.UploadImageAsync(buffer, safeFileName, request.roomType!);
            buffer.Position = 0;
            var thumbnailPath = await _cosService.GenerateThumbnailAsync(buffer, cosPath);

            var image = new Models.Entities.Image
            {
                FileName = safeFileName,
                FilePath = cosPath,
                ThumbnailPath = thumbnailPath,
                FileSize = checked((int)file.Length),
                FileHash = fileHash,
                Room = request.roomType,
                Tags = BuildTagsString(request)
            };
            _context.images.Add(image);
            await _context.SaveChangesAsync(cancellationToken);

            AddAudit("gallery.image.upload", "gallery_image", image.ImageID.ToString(), $"上传普通图库图片 {safeFileName}", new
            {
                image.FileSize,
                RoomType = image.Room,
                imageInfo.Width,
                imageInfo.Height
            });
            await _context.SaveChangesAsync(cancellationToken);

            return OkEnvelope(new
            {
                ImageId = image.ImageID,
                image.FileName,
                RoomType = image.Room,
                Style = request.style,
                image.FileSize,
                ThumbnailUrl = $"/api/Images/{image.ImageID}/file?type=thumbnail",
                OriginalUrl = $"/api/Images/{image.ImageID}/file",
                CreatedAt = image.UploadTime
            }, "图片上传成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "管理员上传图库图片失败. RequestId={RequestId}", HttpContext.TraceIdentifier);
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                Code = 50201,
                Message = "图片存储服务暂时不可用",
                RequestId = HttpContext.TraceIdentifier
            });
        }
    }

    [HttpGet("system")]
    public async Task<IActionResult> GetSystem(CancellationToken cancellationToken)
    {
        var checkedAt = DateTime.UtcNow;
        var databaseTimer = Stopwatch.StartNew();
        var databaseAvailable = false;
        string databaseMessage;
        try
        {
            databaseAvailable = await _context.Database.CanConnectAsync(cancellationToken);
            databaseMessage = databaseAvailable ? "数据库连接正常" : "数据库无法连接";
        }
        catch (Exception ex)
        {
            databaseMessage = "数据库检查失败";
            _logger.LogWarning(ex, "管理员系统页数据库连通性检查失败");
        }
        databaseTimer.Stop();

        var process = Process.GetCurrentProcess();
        var startedAt = process.StartTime.ToUniversalTime();
        var defaultBucket = _configuration["COS:Bucket"];
        var defaultRegion = _configuration["COS:Region"];
        var defaultBaseUrl = _configuration["COS:BaseUrl"];
        var aiBucket = _configuration["COS:AIBucket"];
        var aiRegion = _configuration["COS:AIRegion"];
        var aiBaseUrl = _configuration["COS:AIBaseUrl"];
        var cosCredentialsConfigured = !string.IsNullOrWhiteSpace(_configuration["COS:SecretId"])
            && !string.IsNullOrWhiteSpace(_configuration["COS:SecretKey"]);
        var comfyUrl = _configuration["ComfyUI:ApiUrl"];
        var comfyConfigured = !string.IsNullOrWhiteSpace(comfyUrl);

        return OkEnvelope(new
        {
            Application = new
            {
                Name = "InteriorDesignWeb",
                Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
                Environment = _environment.EnvironmentName,
                ServerTime = checkedAt,
                StartedAt = startedAt,
                Uptime = FormatDuration(checkedAt - startedAt)
            },
            Database = new
            {
                Status = databaseAvailable ? "Healthy" : "Unhealthy",
                Message = databaseMessage,
                LatencyMs = databaseTimer.ElapsedMilliseconds,
                CheckedAt = checkedAt,
                Provider = _context.Database.ProviderName
            },
            Cos = new
            {
                Status = cosCredentialsConfigured && !string.IsNullOrWhiteSpace(defaultBucket) ? "Configured" : "Unconfigured",
                Message = cosCredentialsConfigured ? "COS 凭据与存储桶配置已加载" : "COS 配置不完整",
                LatencyMs = 0,
                CheckedAt = checkedAt,
                Bucket = defaultBucket,
                Region = defaultRegion,
                BaseUrl = defaultBaseUrl,
                AiBucket = new { Bucket = aiBucket, Region = aiRegion, BaseUrl = aiBaseUrl }
            },
            AiProvider = new
            {
                Status = comfyConfigured ? "Configured" : "Unconfigured",
                Message = comfyConfigured ? "AI Provider 地址已配置（未执行外部探测）" : "AI Provider 地址未配置",
                LatencyMs = 0,
                CheckedAt = checkedAt,
                Provider = "ComfyUI",
                Endpoint = comfyUrl
            },
            Runtime = new
            {
                Framework = RuntimeInformation.FrameworkDescription,
                Os = RuntimeInformation.OSDescription,
                ProcessMemoryBytes = process.WorkingSet64,
                ProcessorCount = Environment.ProcessorCount,
                Architecture = RuntimeInformation.ProcessArchitecture.ToString()
            }
        });
    }

    [HttpGet("apis")]
    public IActionResult GetApis()
    {
        var endpoints = _actionDescriptors.ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Select(action =>
            {
                var controllerAuthorize = action.ControllerTypeInfo.GetCustomAttributes<AuthorizeAttribute>(true);
                var actionAuthorize = action.MethodInfo.GetCustomAttributes<AuthorizeAttribute>(true);
                var authorizeAttributes = controllerAuthorize.Concat(actionAuthorize).ToArray();
                var allowAnonymous = action.MethodInfo.GetCustomAttributes<AllowAnonymousAttribute>(true).Any()
                    || action.ControllerTypeInfo.GetCustomAttributes<AllowAnonymousAttribute>(true).Any();
                var roles = authorizeAttributes
                    .SelectMany(attribute => (attribute.Roles ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var methods = action.ActionConstraints?
                    .OfType<HttpMethodActionConstraint>()
                    .SelectMany(constraint => constraint.HttpMethods)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray() ?? Array.Empty<string>();

                return methods.DefaultIfEmpty("ANY").Select(method => new
                {
                    Method = method,
                    Path = "/" + (action.AttributeRouteInfo?.Template ?? string.Empty).TrimStart('/'),
                    Controller = action.ControllerName,
                    Summary = action.DisplayName ?? action.ActionName,
                    Authorization = allowAnonymous
                        ? "Anonymous"
                        : roles.Length > 0
                            ? string.Join(", ", roles)
                            : authorizeAttributes.Length > 0 ? "Authenticated" : "Public"
                });
            })
            .SelectMany(value => value)
            .OrderBy(endpoint => endpoint.Path)
            .ThenBy(endpoint => endpoint.Method)
            .ToList();

        return OkEnvelope(new { Items = endpoints, TotalCount = endpoints.Count });
    }

    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? action = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _context.adminauditlogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            query = query.Where(log =>
                log.Action.Contains(keyword)
                || (log.Summary != null && log.Summary.Contains(keyword))
                || (log.TargetID != null && log.TargetID.Contains(keyword)));
        }
        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(log => log.Action == action.Trim());

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(log => log.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(log => new
            {
                Id = log.AuditLogID,
                OperatorUserId = log.AdministratorUserID,
                OperatorName = log.Administrator != null ? log.Administrator.UserName : null,
                log.Action,
                log.TargetType,
                TargetId = log.TargetID,
                Description = log.Summary,
                Result = log.Succeeded ? "Success" : "Failed",
                log.IpAddress,
                log.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return OkEnvelope(new
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    [HttpGet("gallery/images")]
    public async Task<IActionResult> GetGalleryImages(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _context.images.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            query = query.Where(image => image.FileName.Contains(keyword)
                || (image.Room != null && image.Room.Contains(keyword))
                || (image.Tags != null && image.Tags.Contains(keyword)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(image => image.UploadTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(image => new
            {
                ImageId = image.ImageID,
                image.FileName,
                RoomType = image.Room,
                image.FileSize,
                image.Tags,
                ThumbnailUrl = $"/api/Images/{image.ImageID}/file?type=thumbnail",
                OriginalUrl = $"/api/Images/{image.ImageID}/file",
                CreatedAt = image.UploadTime
            })
            .ToListAsync(cancellationToken);

        return OkEnvelope(new
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    private string? ValidateGalleryUpload(ImageUploadRequest request)
    {
        var file = request.file;
        if (file == null || file.Length <= 0) return "请选择要上传的图片";
        if (file.Length > MaxGalleryUploadBytes) return "图片不能超过 20 MB";
        if (!AllowedContentTypes.Contains(file.ContentType)) return "仅支持 JPEG、PNG 和 WebP 图片";
        if (!AllowedExtensions.Contains(Path.GetExtension(file.FileName))) return "图片扩展名不受支持";
        if (string.IsNullOrWhiteSpace(request.roomType) || !AllowedRoomTypes.Contains(request.roomType.Trim()))
            return "房间类型无效";
        request.roomType = request.roomType.Trim();
        return null;
    }

    private async Task<int> CountEnabledAdministrators(CancellationToken cancellationToken) =>
        await _context.users.CountAsync(
            user => user.IsEnabled && user.Role == UserRole.Administrator,
            cancellationToken);

    private async Task<int> RevokeActiveSessions(int userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return await _context.usersessions
            .Where(session => session.UserID == userId && session.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.RevokedAt, now), cancellationToken);
    }

    private void AddAudit(
        string action,
        string targetType,
        string? targetId,
        string summary,
        object? metadata = null,
        bool succeeded = true,
        string? failureReason = null)
    {
        _context.adminauditlogs.Add(new AdminAuditLog
        {
            AdministratorUserID = GetAdministratorIdOrNull(),
            Action = Truncate(action, 80)!,
            TargetType = Truncate(targetType, 50)!,
            TargetID = Truncate(targetId, 100),
            Summary = Truncate(summary, 300),
            MetadataJson = metadata == null ? null : JsonSerializer.Serialize(metadata),
            IpAddress = Truncate(HttpContext.Connection.RemoteIpAddress?.ToString(), 64),
            UserAgent = Truncate(Request.Headers.UserAgent.ToString(), 300),
            RequestID = Truncate(HttpContext.TraceIdentifier, 100),
            Succeeded = succeeded,
            FailureReason = Truncate(failureReason, 500),
            CreatedAt = DateTime.UtcNow
        });
    }

    private int GetAdministratorId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : 0;

    private int? GetAdministratorIdOrNull()
    {
        var value = GetAdministratorId();
        return value > 0 ? value : null;
    }

    private static bool TryParseRole(string? value, out UserRole role)
    {
        if (string.Equals(value, "RegularUser", StringComparison.OrdinalIgnoreCase))
        {
            role = UserRole.FreeUser;
            return true;
        }
        return Enum.TryParse(value, true, out role) && Enum.IsDefined(role);
    }

    private static string BuildTagsString(ImageUploadRequest request)
    {
        var tags = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.houseType)) tags.Add($"户型:{request.houseType.Trim()}");
        if (!string.IsNullOrWhiteSpace(request.style)) tags.Add($"风格:{request.style.Trim()}");
        if (!string.IsNullOrWhiteSpace(request.material)) tags.Add($"材质:{request.material.Trim()}");
        if (!string.IsNullOrWhiteSpace(request.elements)) tags.Add($"包含元素:{request.elements.Trim()}");
        if (!string.IsNullOrWhiteSpace(request.other)) tags.Add($"其他:{request.other.Trim()}");
        return Truncate(string.Join(';', tags), 1500) ?? string.Empty;
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        var size = Math.Max(0, (double)bytes);
        var index = 0;
        while (size >= 1024 && index < suffixes.Length - 1)
        {
            size /= 1024;
            index++;
        }
        return $"{size:0.##} {suffixes[index]}";
    }

    private static string FormatDuration(TimeSpan value) =>
        $"{(int)value.TotalDays}天 {value.Hours}小时 {value.Minutes}分钟";

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null : value[..Math.Min(value.Length, maxLength)];

    private IActionResult OkEnvelope(object data, string? message = null) =>
        Ok(new { Code = 200, Message = message, Data = data });

    private IActionResult BadRequestEnvelope(string message) =>
        BadRequest(new { Code = 40001, Message = message });

    private IActionResult NotFoundEnvelope(string message) =>
        NotFound(new { Code = 40401, Message = message });

    private IActionResult ConflictEnvelope(string message) =>
        Conflict(new { Code = 40901, Message = message });
}
