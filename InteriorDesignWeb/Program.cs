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

// === [hermes 2026-07-05] 抽取到扩展方法 ===
builder.Services.AddApplicationControllers();
builder.Services.AddSwaggerDocumentation();
builder.Services.AddCorsPolicies(builder.Configuration, builder.Environment);

builder.Services.AddDatabaseServices(builder.Configuration); // hermes 2026-07-05: extracted to DatabaseServiceExtensions

builder.Services.AddJwtAuthentication(builder.Configuration); // hermes 2026-07-05: extracted to AuthenticationServiceExtensions
builder.Services.AddApplicationAuthorization(); // hermes 2026-07-05: extracted to AuthenticationServiceExtensions

builder.Services.AddCloudStorage(builder.Configuration, builder.Environment); // hermes 2026-07-05: extracted to CloudStorageServiceExtensions
builder.Services.AddAIInfrastructure(builder.Configuration, builder.Environment); // hermes 2026-07-05: extracted to AIInfrastructureServiceExtensions

// 添加缓存服务
builder.Services.AddMemoryCache();

// --- [hermes 2026-07-05] removed: Swagger/CORS moved to top, DbContext extracted to DatabaseServiceExtensions ---
// --- [hermes 2026-07-05] removed: ComfyUI HttpClient extracted to AIInfrastructureServiceExtensions ---

// 注册服务
// 添加服务


// --- [hermes 2026-07-05] removed: ComfyUIService/FluxWorkflowService/JobTrackingService extracted to AIInfrastructureServiceExtensions ---

// --- [hermes 2026-07-05] removed: CosService extracted to CloudStorageServiceExtensions ---


// 添加数据库配置
//Console.WriteLine("=== 数据库连接测试 ===");
//Console.WriteLine($"使用连接字符串: {connectionString}");


builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
// --- [hermes 2026-07-05] removed: Quota/RoleLimit/RoleLimits extracted to AIInfrastructureServiceExtensions ---

// --- [hermes 2026-07-05] removed: Cookie/JWT/Authorization registration extracted to AuthenticationServiceExtensions ---

// ----4/22----添加OpenAPI安全定义


// --- [hermes 2026-07-05] removed: old COS config/CosXmlServer/CosService extracted to CloudStorageServiceExtensions ---


var app = builder.Build();

// 添加错误处理中间件（必须放在所有中间件最前面）-26.7.5
app.UseMiddleware<InteriorDesignWeb.Middlewares.ExceptionHandlingMiddleware>();

//----4/10-----//
app.UseStaticFiles(); // 告诉程序可以读取wwwroot里的文件


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
