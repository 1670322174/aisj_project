using InteriorDesignWeb.Config;
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
        var comfyApiUrl = configuration["ComfyUI:ApiUrl"];

        if (string.IsNullOrWhiteSpace(comfyApiUrl))
        {
            throw new InvalidOperationException("ComfyUI:ApiUrl 未配置。");
        }

        services.AddHttpClient("ComfyUI", client =>
        {
            client.BaseAddress = new Uri(comfyApiUrl);
            client.Timeout = TimeSpan.FromMinutes(10);
        })
        .ConfigurePrimaryHttpMessageHandler(() =>
        {
            var handler = new HttpClientHandler();

            if (environment.IsDevelopment())
            {
                handler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }

            handler.UseProxy = false;
            handler.Proxy = null;

            return handler;
        });

        services.Configure<RoleLimitsOptions>(
            configuration.GetSection("RoleLimits")
        );

        services.TryAddScoped<FluxWorkflowService>();
        services.TryAddScoped<JobTrackingService>();
        services.TryAddScoped<ComfyUIService>();

        // AI 任务中心
        services.TryAddScoped<IAIJobRepository, AIJobRepository>();
        services.TryAddScoped<IAIJobService, AIJobService>();

        services.TryAddScoped<IRoleLimitService, RoleLimitService>();
        services.TryAddScoped<RoleLimitService>();

        services.TryAddScoped<IQuotaService, QuotaService>();
        services.TryAddScoped<QuotaService>();

        return services;
    }
}
