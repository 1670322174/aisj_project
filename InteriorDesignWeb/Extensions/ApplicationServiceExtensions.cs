// 作用：集中注册 Controller、Swagger、CORS 等 Web 层基础服务。
// Program.cs 只调用这些扩展方法，避免启动入口堆积大量配置代码。

using System.Text.Json.Serialization;
using InteriorDesignWeb.Filters;
using Microsoft.OpenApi.Models;

namespace InteriorDesignWeb.Extensions;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplicationControllers(this IServiceCollection services)
    {
        services
            .AddControllers()
            .AddJsonOptions(options =>
            {
                // 避免 EF 导航属性循环引用导致接口序列化失败。
                options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            });

        return services;
    }

    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "DesignHub API",
                Version = "v1",
                Description = "AI 室内设计 Web 系统 API"
            });

            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                In = ParameterLocation.Header,
                Description = "请输入 JWT Token，格式：Bearer {token}",
                Name = "Authorization",
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            // 支持 Swagger 正确展示 multipart/form-data 文件上传接口。
            c.OperationFilter<SwaggerFileUploadFilter>();
        });

        return services;
    }

    public static IServiceCollection AddCorsPolicies(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("FrontDev", policy =>
            {
                policy
                    .WithOrigins(
                        "http://localhost:5173",
                        "https://localhost:5173",
                        "http://127.0.0.1:5173",
                        "https://127.0.0.1:5173"
                    )
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            });

            options.AddPolicy("FrontProd", policy =>
            {
                var allowedOrigins = configuration
                    .GetSection("Cors:AllowedOrigins")
                    .Get<string[]>() ?? Array.Empty<string>();

                if (allowedOrigins.Length > 0)
                {
                    policy
                        .WithOrigins(allowedOrigins)
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                }
            });
        });

        return services;
    }
}
