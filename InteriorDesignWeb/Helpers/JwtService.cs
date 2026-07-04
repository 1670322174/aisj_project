using InteriorDesignWeb.Models.Entities;
using System.Security.Claims;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;

namespace InteriorDesignWeb.Helpers
{
    public class JwtService
    {
        private readonly IConfiguration _config;

        public JwtService(IConfiguration config)
        {
            _config = config;
        }

        public string GenerateToken(User user)
        {
            // 修改配置键名
            var jwtSettings = _config.GetSection("JwtSettings").Get<JwtSettings>();

            if (string.IsNullOrEmpty(jwtSettings?.Secret))
                throw new ArgumentNullException("JwtSettings:Secret");

            var securityKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings.Secret));

            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
        new Claim(JwtRegisteredClaimNames.Sub, user.UserID.ToString()),
        new Claim(ClaimTypes.Name, user.UserName),
        new Claim(ClaimTypes.Role, user.Role.ToString()), // 确保转换为字符串
        new Claim("RegisterTime", user.RegisterTime.ToString("O"))
    };

            var token = new JwtSecurityToken(
                issuer: jwtSettings.Issuer,
                audience: jwtSettings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(jwtSettings.ExpireMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
