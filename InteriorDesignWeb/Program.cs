// 作用：ASP.NET Core 应用启动入口。
// 本文件只保留启动流程和中间件管线，具体服务注册统一放到 Extensions 目录中。

using InteriorDesignWeb.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 基础能力注册
builder.Services.AddMemoryCache();
builder.Services.AddApplicationControllers();
builder.Services.AddSwaggerDocumentation();
builder.Services.AddCorsPolicies(builder.Configuration, builder.Environment);

// 基础设施注册
builder.Services.AddDatabaseServices(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddApplicationAuthorization();
builder.Services.AddCloudStorage(builder.Configuration, builder.Environment);
builder.Services.AddAIInfrastructure(builder.Configuration, builder.Environment);

var app = builder.Build();

// 全局异常处理必须尽量靠前，统一返回 ApiResponse 错误结构。
app.UseMiddleware<InteriorDesignWeb.Middlewares.ExceptionHandlingMiddleware>();

// 只暴露 wwwroot 静态资源，避免开发环境暴露项目根目录文件。
app.UseStaticFiles();

app.UseHttpsRedirection();
app.UseRouting();

// 开发环境使用前端本地端口，生产环境使用 appsettings 中 Cors:AllowedOrigins。
app.UseCors(app.Environment.IsDevelopment() ? "FrontDev" : "FrontProd");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// 当前阶段保留 Swagger，方便后端接口联调；上线前可改为仅 Development 启用。
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "DesignHub API v1");
});

app.Run();
