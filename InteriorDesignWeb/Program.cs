using System.Data.SqlClient;
using System.Data;
using MySqlConnector;
using InteriorDesignWeb.Data;
using InteriorDesignWeb.Extensions;
using InteriorDesignWeb.Repositories;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using InteriorDesignWeb.Helpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.Extensions.FileProviders;
using InteriorDesignWeb;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using InteriorDesignWeb.Models.Entities;
using InteriorDesignWeb.Services;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json.Serialization;
using InteriorDesignWeb.Config;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using SixLabors.ImageSharp;
using TencentCloud.Common;
using COSXML;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using COSXML.Auth;
using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using TencentCloud.Lke.V20231130.Models;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
// 添加缓存服务
builder.Services.AddMemoryCache();

// Swagger配置

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "DesignHub API", Version = "v1", Description = "集成ComfyUI工作流的AI图像生成接口" });
    // 安全定义
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "请输入JWT Token，格式：Bearer {token}",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    // 安全需求
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
            new string[]{}
        }
    });
    // swagger添加文件上传支持
    c.OperationFilter<SwaggerFileUploadFilter>();
});


// 添加控制器服务
builder.Services.AddControllers()
    .AddJsonOptions(options => {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });


// 允许所有网页访问接口（开发阶段用，上线后需要调整）
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontDev", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173", "https://localhost:5173") // 精确来源，别写 *
            .AllowAnyMethod()
            .AllowAnyHeader() // 或 .WithHeaders("Authorization","Content-Type","Accept")
            .AllowCredentials(); // 只要前端带凭证/服务端发 Set-Cookie、就要开
    });
});
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();
// 获取配置
var configuration = builder.Configuration;
var connectionString = configuration.GetConnectionString("DesignDB");

// 添加数据库上下文
builder.Services.AddDbContext<DesignHubContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(5, 7, 17))));

// 添加HttpClient
builder.Services.AddHttpClient("ComfyUI", client =>
{
    var apiUrl = builder.Configuration["ComfyUI:ApiUrl"];
    client.BaseAddress = new Uri(apiUrl);
    client.Timeout = TimeSpan.FromMinutes(5);

}).ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();

    // 仅在开发环境忽略证书验证
    if (builder.Environment.IsDevelopment())
    {
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }
    // 腾讯云内网优化
    handler.UseProxy = false;
    handler.Proxy = null;
    return handler;
});

// 注册服务
// 添加服务


// 修改ComfyUIService的注册代码，添加所需的DesignHubContext参数
builder.Services.AddScoped<ComfyUIService>(provider =>
{
        var httpFactory = provider.GetRequiredService<IHttpClientFactory>();
        var workflowService = provider.GetRequiredService<FluxWorkflowService>();
        var cosService = provider.GetRequiredService<CosService>();
        var trackingService = provider.GetRequiredService<JobTrackingService>();
        var logger = provider.GetRequiredService<ILogger<ComfyUIService>>();
        var config = provider.GetRequiredService<IConfiguration>();
        var cache = provider.GetRequiredService<IMemoryCache>();
        var serviceProvider = provider; // 添加IServiceProvider参数
        var context = provider.GetRequiredService<DesignHubContext>(); // 添加DesignHubContext参数
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();  // 注入 IServiceScopeFactory


    return new ComfyUIService(
                httpFactory,
                workflowService,
                cosService,
                trackingService,
                logger,
                config,
                cache,
                serviceProvider,
                context,
                scopeFactory
            );
});

// 配置ComfyUI
builder.Services.AddSingleton<FluxWorkflowService>();
builder.Services.Configure<ComfyUIConfig>(builder.Configuration.GetSection("ComfyUI"));
builder.Services.AddScoped<JobTrackingService>(); // 添加JobTrackingService

builder.Services.AddScoped<CosService>(provider =>
{
    var service = new CosService(
        provider.GetRequiredService<DesignHubContext>(),
        provider.GetRequiredService<IConfiguration>(),

new CosXmlServer(
   new CosXmlConfig.Builder()
       .IsHttps(true)
       .SetRegion(builder.Configuration["COS:Region"])
       .SetDebugLog(true)
       .Build(),
   new DefaultQCloudCredentialProvider(
       builder.Configuration["COS:SecretId"],
       builder.Configuration["COS:SecretKey"],
       600
   )
),
        provider.GetRequiredService<ILogger<CosService>>());

    return service;
});


// 添加数据库配置
//Console.WriteLine("=== 数据库连接测试 ===");
//Console.WriteLine($"使用连接字符串: {connectionString}");


builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
// 自定义服务注册
builder.Services.AddScoped<IRoleLimitService, RoleLimitService>();
builder.Services.AddScoped<IQuotaService, QuotaService>();
// 绑定配置到类
builder.Services.Configure<RoleLimitsOptions>(
    builder.Configuration.GetSection("RoleLimits")
);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
    });

//// 添加配置验证（可选）
//builder.Services.AddOptions<RoleLimitsOptions>()
//    .BindConfiguration("RoleLimits")
//    .Validate(options => options.IsValid(), "角色限制配置不完整");


// 添加JWT认证服务----4/13-----//
// 添加JWT配置

// 添加认证服务
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
// 修改以下代码以确保从配置中获取的值不为 null  
.AddJwtBearer(options =>
{
    var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>();

    // 确保 jwtSettings 对象不为 null 且其属性有效
    if (jwtSettings == null || string.IsNullOrEmpty(jwtSettings.Secret) || string.IsNullOrEmpty(jwtSettings.Issuer) || string.IsNullOrEmpty(jwtSettings.Audience))
    {
        throw new InvalidOperationException("JwtSettings 配置不完整或缺失。");
    }
    // 打印配置信息，确保正确读取----4/23----
    Console.WriteLine($"JWT配置详情:\nSecret: {jwtSettings.Secret?.Substring(0, Math.Min(4, jwtSettings.Secret.Length))}...\nIssuer: {jwtSettings.Issuer}\nAudience: {jwtSettings.Audience}");
    
    Console.WriteLine($"JWT配置验证：\n" +
                  $"Secret长度：{jwtSettings.Secret?.Length ?? 0}\n" +
                  $"Issuer：{jwtSettings.Issuer}\n" +
                  $"Audience：{jwtSettings.Audience}");
    // 确保 Secret 不为 null  
    var secret = builder.Configuration["JwtSettings:Secret"];
    if (string.IsNullOrEmpty(secret))
    {
        throw new InvalidOperationException("JwtSettings:Secret 配置项不能为空。");
    }

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
        ValidAudience = builder.Configuration["JwtSettings:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
    };

    // 处理未授权响应  
    options.Events = new JwtBearerEvents
    {

        OnTokenValidated = context =>
        {
            if (context.Principal != null)
            {
                Console.WriteLine("认证成功用户：");
                Console.WriteLine($"ID：{context.Principal.FindFirstValue(ClaimTypes.NameIdentifier)}");
                Console.WriteLine($"角色：{context.Principal.FindFirstValue(ClaimTypes.Role)}");
            }
            else
            {
                Console.WriteLine("认证成功，但 Principal 为 null。");
            }
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            
            Console.WriteLine("认证失败原因：");
            Console.WriteLine(context.Exception.Message);

            if (context.Principal == null)
            {
                Console.WriteLine("Authentication failed: Principal is null."); 
                return Task.CompletedTask;
            }
            var roleClaim = context.Principal.FindFirst(c =>
                c.Type.Contains("role", StringComparison.OrdinalIgnoreCase));
            Console.WriteLine($"实际解析的角色声明：{roleClaim?.Type}={roleClaim?.Value}");
            return Task.CompletedTask;
        },
    };
});

builder.Services.AddAuthorization(options =>
{
    // 简化策略：每个策略仅要求单一角色
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

// ----4/22----添加OpenAPI安全定义


// ----5/22----腾讯云COS配置
builder.Configuration.AddJsonFile("appsettings.json");

// ======== COS 服务配置 ========
var cosConfig = builder.Configuration.GetSection("COS");


// 注册 COS 客户端（单例）
builder.Services.AddSingleton(provider =>
{
    var config = new CosXmlConfig.Builder()
        .IsHttps(true)
        .SetRegion(cosConfig["Region"])
        .SetDebugLog(true)
        .Build();

    // 使用临时密钥提供器
    //var credentialProvider = new CustomQCloudCredentialProvider(
    //    cosConfig["TempSecretId"],
    //    cosConfig["TempSecretKey"],
    //    cosConfig["Token"]
    //);
    // 使用永久密钥
    var credentialProvider = new DefaultQCloudCredentialProvider(
        cosConfig["SecretId"],
        cosConfig["SecretKey"],
        600
    );

    return new CosXmlServer(config, credentialProvider);
});
// 1. 定义 CORS 策略            "https://localhost:5001" // 如有 https（开发阶段用，上线后需要调整）
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});


// ======== 其他服务配置 ========
builder.Services.AddDbContext<DesignHubContext>(/* 数据库配置 */);
builder.Services.AddControllers();
//builder.Services.AddScoped<CosService>();


var app = builder.Build();

//----4/10-----//
app.UseStaticFiles(); // 告诉程序可以读取wwwroot里的文件



// 添加错误处理中间件（必须放在所有中间件最前面）
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";

        var error = new
        {
            Success = false,
            Message = "发生未处理的系统错误",
            RequestId = Activity.Current?.Id ?? context.TraceIdentifier
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(error));
    });
});


// 配置静态文件访问（本地存储）
//app.UseStaticFiles(new StaticFileOptions
//{
//    FileProvider = new PhysicalFileProvider(
//        Path.Combine(builder.Environment.ContentRootPath, "C:\\InteriorDesignImages")),
//    RequestPath = "/files"
//});




// 允许访问任意路径（仅开发环境）
if (app.Environment.IsDevelopment())
{
    app.UseStaticFiles(new StaticFileOptions
    {
        ServeUnknownFileTypes = true,
        FileProvider = new PhysicalFileProvider(
            Path.Combine(Directory.GetCurrentDirectory())),
        RequestPath = ""
    });
}
// 2. 在中间件中启用---cos服务配置
app.UseCors("DevAll");

app.UseHttpsRedirection();
app.UseRouting();

// 关键顺序 ↓
app.UseCors("FrontDev");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Swagger中间件
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "AI API v1"));

app.Run();

// 添加自定义异常过滤器 ----4/15-----//
builder.Services.AddControllers(options =>
{
options.Filters.Add<HttpResponseExceptionFilter>();
});

// 创建异常过滤器
public class HttpResponseExceptionFilter : IActionFilter, IOrderedFilter
{
    public int Order => int.MaxValue - 10;

    public void OnActionExecuting(ActionExecutingContext context) { }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        if (context.Exception is not null)
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILogger<HttpResponseExceptionFilter>>();

            logger.LogError(context.Exception, "全局异常捕获");

            context.Result = new ObjectResult(new
            {
                Code = 50000,
                Message = "系统发生未处理异常",
                Detail = context.Exception.Message
            })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };

            context.ExceptionHandled = true;
        }
    }
}

// 文件上传接口的Swagger配置 ----5/22-----//
public class SwaggerFileUploadFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.GetCustomAttribute<HttpPostAttribute>()?.Template == "upload")
        {
            operation.Parameters.Clear();
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "file",
                In = ParameterLocation.Header,
                Description = "上传图片文件",
                Required = true,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["multipart/form-data"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties =
                            {
                                ["file"] = new OpenApiSchema
                                {
                                    Type = "string",
                                    Format = "binary"
                                }
                            }
                        }
                    }
                }
            });
        }
    }
}
public class ComfyUIConfig
{
    public required string ApiUrl { get; set; }
    public required string WorkflowName { get; set; }
}
