// 作用：集中注册 AI 工作流、ComfyUI Server Provider、任务中心和额度服务。
// 当前后端只保留一条 ComfyUI Server 执行链路，不注册 Comfy Cloud 或旧 /api/flux 服务。

using System.Net.Http.Headers;
using InteriorDesignWeb.Config;
using InteriorDesignWeb.Providers.AI;
using InteriorDesignWeb.Repositories.AI;
using InteriorDesignWeb.Services;
using InteriorDesignWeb.Services.AI;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InteriorDesignWeb.Extensions;

public static class AIInfrastructureServiceExtensions
{
    public static IServiceCollection AddAIInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var section = configuration.GetSection(ComfyUIServerOptions.SectionName);
        var apiUrl = section["ApiUrl"];
        var accountApiKey = section["AccountApiKey"];
        var authorizationHeader = section["AuthorizationHeader"];

        if (string.IsNullOrWhiteSpace(apiUrl)
            || !Uri.TryCreate(EnsureTrailingSlash(apiUrl), UriKind.Absolute, out var serverBaseUri))
        {
            throw new InvalidOperationException(
                "ComfyUI:ApiUrl 未配置或格式无效，例如 http://192.168.1.20:8188/。");
        }

        // 当前 7 个工作流均包含 Partner Nodes，因此启动时要求配置账号 API Key。
        if (string.IsNullOrWhiteSpace(accountApiKey))
        {
            throw new InvalidOperationException(
                "ComfyUI:AccountApiKey 未配置。建议使用环境变量 ComfyUI__AccountApiKey。");
        }

        services.Configure<ComfyUIServerOptions>(section);

        services.AddHttpClient("ComfyUI", client =>
        {
            client.BaseAddress = serverBaseUri;
            client.Timeout = TimeSpan.FromMinutes(
                int.TryParse(section["RequestTimeoutMinutes"], out var minutes)
                    ? Math.Clamp(minutes, 1, 60)
                    : 15);
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            // 可选：远程 ComfyUI 前方使用 Nginx/网关时，为入口添加 Bearer 或 Basic 鉴权。
            if (!string.IsNullOrWhiteSpace(authorizationHeader))
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation(
                    "Authorization",
                    authorizationHeader);
            }
        })
        .ConfigurePrimaryHttpMessageHandler(() =>
        {
            var useProxy = bool.TryParse(section["UseProxy"], out var proxyEnabled)
                && proxyEnabled;
            var allowInvalidCertificate = bool.TryParse(
                    section["AllowInvalidCertificate"],
                    out var certificateEnabled)
                && certificateEnabled;

            var handler = new HttpClientHandler
            {
                UseProxy = useProxy,
                Proxy = null
            };

            // 只允许开发环境显式跳过自签名证书验证，生产环境禁止。
            if (environment.IsDevelopment() && allowInvalidCertificate)
            {
                handler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }

            return handler;
        });

        services.Configure<RoleLimitsOptions>(
            configuration.GetSection("RoleLimits"));

        services.TryAddScoped<IAIJobRepository, AIJobRepository>();
        services.TryAddScoped<IAIJobService, AIJobService>();

        services.TryAddSingleton<IWorkflowRegistry, WorkflowRegistry>();
        services.TryAddScoped<IWorkflowBuilder, WorkflowBuilder>();
        services.TryAddScoped<IAIProvider, ComfyUIServerProvider>();
        services.TryAddScoped<IAIResultService, AIResultService>();
        services.TryAddScoped<IAIGenerationService, AIGenerationService>();
        services.AddHostedService<AIJobBackgroundWorker>();

        services.TryAddScoped<IRoleLimitService, RoleLimitService>();
        services.TryAddScoped<RoleLimitService>();
        services.TryAddScoped<IQuotaService, QuotaService>();
        services.TryAddScoped<QuotaService>();
        services.TryAddScoped<IUsageQuotaService, UsageQuotaService>();

        return services;
    }

    private static string EnsureTrailingSlash(string value)
    {
        return value.EndsWith('/') ? value : value + "/";
    }
}
