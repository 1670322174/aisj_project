// 作用：集中注册 AI 生图相关基础设施。
// 当前阶段保留已有 ComfyUI / Flux 工作流能力，同时注册新的 AIJob 任务中心和 7 个工作流接入地基。
// 7 个工作流通过 WorkflowRegistry + WorkflowBuilder 配置化接入，避免在 Controller 中堆 if/else。

using InteriorDesignWeb.Config;
using InteriorDesignWeb.Repositories.AI;
using InteriorDesignWeb.Services;
using InteriorDesignWeb.Services.AI;
using InteriorDesignWeb.Providers.AI;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InteriorDesignWeb.Extensions;

public static class AIInfrastructureServiceExtensions
{
    public static IServiceCollection AddAIInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var comfyApiUrl = configuration["ComfyUI:ApiUrl"];

        if (string.IsNullOrWhiteSpace(comfyApiUrl))
        {
            throw new InvalidOperationException("ComfyUI:ApiUrl 未配置。");
        }

        // ComfyUI 作为当前阶段真实生图 Provider，后续会降级为 ComfyUIProvider / Client。
        services.AddHttpClient("ComfyUI", client =>
        {
            client.BaseAddress = new Uri(comfyApiUrl);
            client.Timeout = TimeSpan.FromMinutes(10);
        })
        .ConfigurePrimaryHttpMessageHandler(() =>
        {
            var handler = new HttpClientHandler
            {
                UseProxy = false,
                Proxy = null
            };

            if (environment.IsDevelopment())
            {
                handler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }

            return handler;
        });

        services.Configure<RoleLimitsOptions>(
            configuration.GetSection("RoleLimits")
        );

        // 旧 AI 生图链路：当前仍保留，保证 /api/flux/* 不被破坏。
        services.TryAddScoped<FluxWorkflowService>();
        services.TryAddScoped<JobTrackingService>();
        services.TryAddScoped<ComfyUIService>();

        // 新 AI 任务中心：后续作为统一主入口。
        services.TryAddScoped<IAIJobRepository, AIJobRepository>();
        services.TryAddScoped<IAIJobService, AIJobService>();

        // 7 个工作流接入地基：工作流注册、工作流构建、Provider 抽象、结果保存和统一提交服务。
        services.TryAddSingleton<IWorkflowRegistry, WorkflowRegistry>();
        services.TryAddScoped<IWorkflowBuilder, WorkflowBuilder>();
        services.TryAddScoped<IAIProvider, ComfyUIProvider>();
        services.TryAddScoped<IAIResultService, AIResultService>();
        services.TryAddScoped<IAIGenerationService, AIGenerationService>();

        // 额度和角色限制：必须同时注册接口和实现，兼容现有 Controller / Service 注入方式。
        services.TryAddScoped<IRoleLimitService, RoleLimitService>();
        services.TryAddScoped<RoleLimitService>();

        services.TryAddScoped<IQuotaService, QuotaService>();
        services.TryAddScoped<QuotaService>();

        return services;
    }
}
