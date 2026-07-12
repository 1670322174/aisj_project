// 作用：ASP.NET Core 应用启动入口。
// 本文件只保留启动流程和中间件管线，具体服务注册统一放到 Extensions 目录中。

using InteriorDesignWeb.Extensions;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Windows EventLog may be unavailable in local development or restricted
// hosting environments. A logging failure must never abort an API request.
builder.Logging.ClearProviders();
if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddSimpleConsole(options => options.SingleLine = true);
    builder.Logging.AddDebug();
}
else
{
    builder.Logging.AddJsonConsole();
}

// 基础能力注册
builder.Services.AddMemoryCache();
builder.Services.AddApplicationControllers();
builder.Services.AddSwaggerDocumentation();
builder.Services.AddCorsPolicies(builder.Configuration, builder.Environment);
builder.Services.AddApplicationRateLimiting();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
});

// 基础设施注册
builder.Services.AddDatabaseServices(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddApplicationAuthorization();
builder.Services.AddCloudStorage(builder.Configuration, builder.Environment);
builder.Services.AddAIInfrastructure(builder.Configuration, builder.Environment);

var app = builder.Build();

// 在 HTTPS 跳转、日志和登录限流之前恢复同机可信反向代理传来的客户端 IP/协议。
app.UseForwardedHeaders();

// 全局异常处理必须尽量靠前，统一返回 ApiResponse 错误结构。
app.UseMiddleware<InteriorDesignWeb.Middlewares.ExceptionHandlingMiddleware>();
app.UseMiddleware<InteriorDesignWeb.Middlewares.RequestTelemetryMiddleware>();
app.UseHttpsRedirection();

// Vite 的唯一生产输出目录是 wwwroot/dist。不要直接暴露整个 wwwroot，
// 否则 src、配置文件和前端工程文件也会成为可访问的静态资源。
var webRootPath = app.Environment.WebRootPath
    ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
var frontendDistPath = Path.Combine(webRootPath, "dist");
var frontendIndexPath = Path.Combine(frontendDistPath, "index.html");
var hasFrontendBuild = File.Exists(frontendIndexPath);
if (hasFrontendBuild)
{
    var frontendFiles = new PhysicalFileProvider(frontendDistPath);
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = frontendFiles
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = frontendFiles,
        OnPrepareResponse = context =>
        {
            // 单文件构建的 index.html 每次发布都会变化，不能长期缓存。
            if (context.File.Name.Equals("index.html", StringComparison.OrdinalIgnoreCase))
            {
                context.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            }
        }
    });
}
else
{
    app.Logger.LogWarning(
        "Frontend build not found at {FrontendIndexPath}. Run npm run build before starting a production deployment.",
        frontendIndexPath);
}

app.UseRouting();

// 开发环境使用前端本地端口，生产环境使用 appsettings 中 Cors:AllowedOrigins。
app.UseCors(app.Environment.IsDevelopment() ? "FrontDev" : "FrontProd");
app.UseRateLimiter();
app.UseMiddleware<InteriorDesignWeb.Middlewares.AdminRequestProtectionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

// 当前阶段保留 Swagger，方便后端接口联调；上线前可改为仅 Development 启用。
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "DesignHub API v1");
    });
}

// React Router 使用浏览器历史路由。只有非 API 的未知 GET/HEAD 路径
// 才回退到前端入口，避免不存在的 API 错误地返回 HTML 200。
app.MapFallback(async context =>
{
    var path = context.Request.Path;
    var isBackendPath = path.StartsWithSegments("/api")
        || path.StartsWithSegments("/health")
        || path.StartsWithSegments("/swagger");
    var isPageRequest = HttpMethods.IsGet(context.Request.Method)
        || HttpMethods.IsHead(context.Request.Method);
    var looksLikeStaticFile = Path.HasExtension(path.Value);

    if (!hasFrontendBuild || isBackendPath || !isPageRequest || looksLikeStaticFile)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    context.Response.ContentType = "text/html; charset=utf-8";
    context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
    await context.Response.SendFileAsync(frontendIndexPath);
});

app.Run();
