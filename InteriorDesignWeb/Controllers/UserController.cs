using InteriorDesignWeb.Data;
using InteriorDesignWeb.Helpers;
using InteriorDesignWeb.Models.DTOs;
using InteriorDesignWeb.Models.Entities;
using InteriorDesignWeb.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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
        /// <summary>
        /// 构造函数，注入DesignHubContext依赖
        /// </summary>
        /// <param name="context">数据库上下文实例</param>
        public UserController(IOptions<JwtSettings> jwtSettings, DesignHubContext context, ILogger<UserController> logger)
        {
            _jwtSettings = jwtSettings.Value;
            _context = context; // 初始化数据库上下文
            _logger = logger;
        }
        /// <summary>
        /// 用户注册接口
        /// </summary>
        /// <param name="request">注册请求参数，包含用户名、密码等信息</param>
        /// <returns>返回注册结果</returns>
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
                    Detail = ex.InnerException?.Message
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
                    return Unauthorized(new { Code = 40101, Message = "用户名不存在" });

                // 验证密码
                var hasher = new PasswordHasher<User>();
                var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

                if (result != PasswordVerificationResult.Success)
                    return Unauthorized(new { Code = 40102, Message = "密码错误" });

                // 生成JWT
                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                    new Claim(ClaimTypes.Name, user.UserName),
                    new Claim(ClaimTypes.Role, user.Role.ToString()), // 新增角色声明
                    new Claim(ClaimTypes.MobilePhone, user.PhoneNumber), // 新增手机号声明
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())// 新增JWT ID声明
                };

                var secret = _jwtSettings.Secret ?? throw new ArgumentNullException(nameof(_jwtSettings.Secret), "JWT Secret cannot be null.");
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var token = new JwtSecurityToken(
                    issuer: _jwtSettings.Issuer,
                    audience: _jwtSettings.Audience,
                    claims: claims,
                    expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpireMinutes),
                    signingCredentials: creds
                );

                return Ok(new
                {
                    Code = 200,
                    Message = "登录成功",
                    Data = new
                    {
                        Token = new JwtSecurityTokenHandler().WriteToken(token),
                        Expires = token.ValidTo,
                        UserInfo = new
                        {
                            user.UserID,
                            user.UserName,
                            user.PhoneNumber, // 新增手机号
                            user.Role // 新增角色
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
    }
}

