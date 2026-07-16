// 作用：ASP.NET Core 应用启动入口。
// 本文件只保留启动流程和中间件管线，具体服务注册统一放到 Extensions 目录中。

using InteriorDesignWeb.Extensions;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.FileProviders;
using System.Net;

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
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat([
        "application/json",
        "image/svg+xml"
    ]);
});
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(180);
    options.IncludeSubDomains = false;
});

var dataProtection = builder.Services
    .AddDataProtection()
    .SetApplicationName("InteriorDesignWeb");
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    var resolvedKeysPath = Path.IsPathRooted(dataProtectionKeysPath)
        ? dataProtectionKeysPath
        : Path.Combine(builder.Environment.ContentRootPath, dataProtectionKeysPath);
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(resolvedKeysPath));
}

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    foreach (var proxy in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
    {
        if (IPAddress.TryParse(proxy, out var address) && !options.KnownProxies.Contains(address))
        {
            options.KnownProxies.Add(address);
        }
    }
});

// 基础设施注册
builder.Services.AddDatabaseServices(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddApplicationAuthorization();
builder.Services.AddCloudStorage(builder.Configuration, builder.Environment);
builder.Services.AddAIInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddAssistantInfrastructure(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    if (string.Equals(builder.Configuration["AllowedHosts"], "*", StringComparison.Ordinal))
    {
        app.Logger.LogWarning(
            "AllowedHosts is wildcard in Production. Set AllowedHosts to the public domain name.");
    }
    if (string.IsNullOrWhiteSpace(dataProtectionKeysPath))
    {
        app.Logger.LogWarning(
            "DataProtection:KeysPath is not configured. Search cursors may become invalid after a restart or server replacement.");
    }
}

// 在 HTTPS 跳转、日志和登录限流之前恢复同机可信反向代理传来的客户端 IP/协议。
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// 请求日志位于异常处理器外层，确保记录的是异常转换后的真实状态码。
app.UseMiddleware<InteriorDesignWeb.Middlewares.RequestTelemetryMiddleware>();
app.UseMiddleware<InteriorDesignWeb.Middlewares.ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
if (!app.Environment.IsDevelopment())
{
    // BrowserLink / dotnet watch need to inject their development scripts into HTML.
    // Brotli-compressed responses prevent that injection and only create noisy warnings locally.
    app.UseResponseCompression();
}

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
    if (!app.Environment.IsDevelopment()
        && builder.Configuration.GetValue("Hosting:RequireFrontendBuild", true))
    {
        throw new InvalidOperationException(
            $"Production frontend build is missing: {frontendIndexPath}. Publish the complete application package instead of backend files only.");
    }
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
