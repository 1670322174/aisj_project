using System.Security.Claims;
using System.Text;
using InteriorDesignWeb.Models.Entities;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace InteriorDesignWeb.Extensions;

public static class AuthenticationServiceExtensions
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var secret = configuration["JwtSettings:Secret"];
        var issuer = configuration["JwtSettings:Issuer"];
        var audience = configuration["JwtSettings:Audience"];

        if (string.IsNullOrWhiteSpace(secret)
            || string.IsNullOrWhiteSpace(issuer)
            || string.IsNullOrWhiteSpace(audience))
        {
            throw new InvalidOperationException("JwtSettings 配置不完整，请检查 Secret、Issuer、Audience。");
        }

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.LoginPath = "/Account/Login";
                options.AccessDeniedPath = "/Account/AccessDenied";
            })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                    ClockSkew = TimeSpan.FromMinutes(2)
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("JwtAuthentication");

                        var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                        var role = context.Principal?.FindFirstValue(ClaimTypes.Role);

                        logger.LogDebug(
                            "JWT认证成功。UserId={UserId}, Role={Role}",
                            userId,
                            role
                        );

                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("JwtAuthentication");

                        logger.LogWarning(
                            context.Exception,
                            "JWT认证失败。Path={Path}",
                            context.HttpContext.Request.Path
                        );

                        return Task.CompletedTask;
                    }
                };
            });

        return services;
    }

    public static IServiceCollection AddApplicationAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy("FreeUser", policy =>
                policy.RequireRole(UserRole.FreeUser.ToString()));

            options.AddPolicy("Member", policy =>
                policy.RequireRole(UserRole.Member.ToString()));

            options.AddPolicy("PremiumMember", policy =>
                policy.RequireRole(UserRole.PremiumMember.ToString()));

            options.AddPolicy("Administrator", policy =>
                policy.RequireRole(UserRole.Administrator.ToString()));

            options.InvokeHandlersAfterFailure = true;
        });

        return services;
    }
}
