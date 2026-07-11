using InteriorDesignWeb.Data;
using InteriorDesignWeb.Helpers;
using InteriorDesignWeb.Models.DTOs;
using InteriorDesignWeb.Models.Entities;
using InteriorDesignWeb.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;


namespace InteriorDesignWeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly DesignHubContext _context;
        private readonly JwtSettings _jwtSettings;
        private readonly ILogger<UserController> _logger;
        private readonly IWebHostEnvironment _environment;
        private const string AccessCookieName = "designhub_access";
        private const string RefreshCookieName = "designhub_refresh";
        private static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(14);
        private static readonly User DummyUser = new() { UserName = "timing-protection" };
        private static readonly string DummyPasswordHash =
            new PasswordHasher<User>().HashPassword(DummyUser, "not-a-real-password");
        /// <summary>
        /// 构造函数，注入DesignHubContext依赖
        /// </summary>
        /// <param name="context">数据库上下文实例</param>
        public UserController(
            IOptions<JwtSettings> jwtSettings,
            DesignHubContext context,
            ILogger<UserController> logger,
            IWebHostEnvironment environment)
        {
            _jwtSettings = jwtSettings.Value;
            _context = context; // 初始化数据库上下文
            _logger = logger;
            _environment = environment;
        }
        /// <summary>
        /// 用户注册接口
        /// </summary>
        /// <param name="request">注册请求参数，包含用户名、密码等信息</param>
        /// <returns>返回注册结果</returns>
        [Authorize(Roles = nameof(UserRole.Administrator))]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                // 参数校验
                if (!ModelState.IsValid)
                    return BadRequest(new { Code = 40001, Message = "参数格式错误" });

                // 检查用户名是否已存在
                if (await _context.users.AnyAsync(u => u.UserName == request.Username))
                    return Conflict(new { Code = 40901, Message = "用户名已被注册" });

                // 检查手机号是否已存在
                if (await _context.users.AnyAsync(u => u.PhoneNumber == request.PhoneNumber))
                    return Conflict(new { Code = 40902, Message = "手机号已被注册" });

                // 创建用户
                var newUser = new User
                {
                    UserName = request.Username,
                    PhoneNumber = request.PhoneNumber,
                    RegisterTime = DateTime.UtcNow
                };

                // 密码加密
                var hasher = new PasswordHasher<User>();
                newUser.PasswordHash = hasher.HashPassword(newUser, request.Password);

                // 保存用户
                _context.users.Add(newUser);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Code = 200,
                    Message = "注册成功",
                    Data = new { UserId = newUser.UserID }
                });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "数据库保存失败");
                return StatusCode(500, new
                {
                    Code = 50001,
                    Message = "数据库操作异常",
                    RequestId = HttpContext.TraceIdentifier
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "未知错误");
                return StatusCode(500, new
                {
                    Code = 50000,
                    Message = "系统内部错误"
                });
            }
        }

        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                // 参数校验
                if (!ModelState.IsValid)
                    return BadRequest(new { Code = 40001, Message = "参数格式错误" });

                // 查询用户
                var user = await _context.users
                    .FirstOrDefaultAsync(u => u.UserName == request.Username);

                if (user == null)
                {
                    // Spend the same password verification work for an unknown
                    // account to reduce username discovery through response timing.
                    _ = new PasswordHasher<User>().VerifyHashedPassword(
                        DummyUser,
                        DummyPasswordHash,
                        request.Password);
                    return Unauthorized(new { Code = 40101, Message = "用户名或密码错误" });
                }

                // 验证密码
                var hasher = new PasswordHasher<User>();
                var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

                if (result == PasswordVerificationResult.Failed)
                    return Unauthorized(new { Code = 40101, Message = "用户名或密码错误" });

                if (result == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    user.PasswordHash = hasher.HashPassword(user, request.Password);
                    await _context.SaveChangesAsync();
                }

                var now = DateTime.UtcNow;
                var refreshToken = CreateRefreshToken();
                var refreshExpires = now.Add(RefreshLifetime);
                _context.usersessions.Add(new UserSession
                {
                    UserID = user.UserID,
                    TokenHash = HashRefreshToken(refreshToken),
                    CreatedAt = now,
                    ExpiresAt = refreshExpires,
                    UserAgent = Truncate(Request.Headers.UserAgent.ToString(), 300),
                    IpAddress = Truncate(HttpContext.Connection.RemoteIpAddress?.ToString(), 64)
                });
                await _context.SaveChangesAsync();

                var accessToken = CreateAccessToken(user);
                SetAuthCookies(accessToken.Value, accessToken.Expires, refreshToken, refreshExpires);

                return Ok(new
                {
                    Code = 200,
                    Message = "登录成功",
                    Data = new
                    {
                        Expires = accessToken.Expires,
                        SessionExpires = refreshExpires,
                        UserInfo = new
                        {
                            user.UserID,
                            user.UserName,
                            user.PhoneNumber, // 新增手机号
                            Role = user.Role.ToString()
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "登录异常");
                return StatusCode(500, new
                {
                    Code = 50000,
                    Message = "登录服务暂时不可用"
                });
            }
        }

        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh()
        {
            if (!Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken)
                || string.IsNullOrWhiteSpace(refreshToken))
            {
                return Unauthorized(new { Code = 40102, Message = "登录状态已过期" });
            }

            var now = DateTime.UtcNow;
            var tokenHash = HashRefreshToken(refreshToken);
            var session = await _context.usersessions
                .Include(item => item.User)
                .FirstOrDefaultAsync(item => item.TokenHash == tokenHash);

            if (session == null || session.RevokedAt != null || session.ExpiresAt <= now)
            {
                return Unauthorized(new { Code = 40102, Message = "登录状态已过期" });
            }

            var replacementToken = CreateRefreshToken();
            var replacementHash = HashRefreshToken(replacementToken);
            var refreshExpires = now.Add(RefreshLifetime);

            session.LastUsedAt = now;
            session.RevokedAt = now;
            session.ReplacedByTokenHash = replacementHash;
            _context.usersessions.Add(new UserSession
            {
                UserID = session.UserID,
                TokenHash = replacementHash,
                CreatedAt = now,
                ExpiresAt = refreshExpires,
                UserAgent = Truncate(Request.Headers.UserAgent.ToString(), 300),
                IpAddress = Truncate(HttpContext.Connection.RemoteIpAddress?.ToString(), 64)
            });
            await _context.SaveChangesAsync();

            var accessToken = CreateAccessToken(session.User);
            SetAuthCookies(accessToken.Value, accessToken.Expires, replacementToken, refreshExpires);

            return Ok(new
            {
                Code = 200,
                Data = new
                {
                    Expires = accessToken.Expires,
                    SessionExpires = refreshExpires
                }
            });
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(idValue, out var userId)) return Unauthorized();

            var user = await _context.users
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.UserID == userId);
            if (user == null) return Unauthorized();

            return Ok(new
            {
                Code = 200,
                Data = new
                {
                    user.UserID,
                    user.UserName,
                    user.PhoneNumber,
                    Role = user.Role.ToString()
                }
            });
        }

        [AllowAnonymous]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            if (Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken)
                && !string.IsNullOrWhiteSpace(refreshToken))
            {
                var tokenHash = HashRefreshToken(refreshToken);
                var session = await _context.usersessions
                    .FirstOrDefaultAsync(item => item.TokenHash == tokenHash && item.RevokedAt == null);
                if (session != null)
                {
                    session.RevokedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }

            DeleteAuthCookies();
            return NoContent();
        }

        private (string Value, DateTime Expires) CreateAccessToken(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };
            var secret = _jwtSettings.Secret
                ?? throw new InvalidOperationException("JWT Secret is not configured.");
            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                SecurityAlgorithms.HmacSha256);
            var expires = DateTime.UtcNow.AddMinutes(
                Math.Clamp(_jwtSettings.ExpireMinutes, 15, 120));
            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: expires,
                signingCredentials: credentials);
            return (new JwtSecurityTokenHandler().WriteToken(token), expires);
        }

        private void SetAuthCookies(
            string accessToken,
            DateTime accessExpires,
            string refreshToken,
            DateTime refreshExpires)
        {
            var secure = !_environment.IsDevelopment();
            Response.Cookies.Append(AccessCookieName, accessToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = secure,
                SameSite = SameSiteMode.Strict,
                Path = "/",
                Expires = accessExpires
            });
            Response.Cookies.Append(RefreshCookieName, refreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = secure,
                SameSite = SameSiteMode.Strict,
                Path = "/api/User",
                Expires = refreshExpires
            });
        }

        private void DeleteAuthCookies()
        {
            var options = new CookieOptions
            {
                HttpOnly = true,
                Secure = !_environment.IsDevelopment(),
                SameSite = SameSiteMode.Strict,
                Path = "/"
            };
            Response.Cookies.Delete(AccessCookieName, options);
            options.Path = "/api/User";
            Response.Cookies.Delete(RefreshCookieName, options);
        }

        private static string CreateRefreshToken() =>
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        private static string HashRefreshToken(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)))
                .ToLowerInvariant();

        private static string? Truncate(string? value, int maxLength) =>
            string.IsNullOrWhiteSpace(value)
                ? null
                : value[..Math.Min(value.Length, maxLength)];
    }
}

