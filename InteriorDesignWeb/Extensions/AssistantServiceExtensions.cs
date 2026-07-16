using InteriorDesignWeb.Config;
using InteriorDesignWeb.Services.Assistant;
using InteriorDesignWeb.Services.Assistant.Models;

namespace InteriorDesignWeb.Extensions;

public static class AssistantServiceExtensions
{
    public static IServiceCollection AddAssistantInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(AssistantOptions.SectionName);
        services.Configure<AssistantOptions>(section);
        services.Configure<AgentModelsOptions>(
            configuration.GetSection(AgentModelsOptions.SectionName));
        services.Configure<AgentPlatformOptions>(
            configuration.GetSection(AgentPlatformOptions.SectionName));
        services.AddHttpContextAccessor();
        services.AddHttpClient<IAssistantModelClient, OpenAICompatibleAssistantModelClient>(client =>
        {
            if (Uri.TryCreate(section["BaseUrl"], UriKind.Absolute, out var baseUri))
            {
                var value = baseUri.AbsoluteUri.EndsWith('/') ? baseUri : new Uri(baseUri.AbsoluteUri + "/");
                client.BaseAddress = value;
            }
            client.Timeout = TimeSpan.FromSeconds(
                int.TryParse(section["TimeoutSeconds"], out var seconds)
                    ? Math.Clamp(seconds, 15, 180)
                    : 90);
        });
        services.AddSingleton<IAgentModelProviderClient, MiniMaxAnthropicAgentModelClient>();
        services.AddSingleton<IAgentModelProviderClient, DeepSeekAgentModelClient>();
        services.AddSingleton<IAgentModelProviderClient, VolcArkResponsesAgentModelClient>();
        services.AddSingleton<IAgentModelRouter, AgentModelRouter>();
        services.AddSingleton<IAgentConfigurationCatalog, AgentConfigurationCatalog>();
        services.AddHostedService<AgentConfigurationStartupValidator>();
        services.AddScoped<IAgentRuntimeService, AgentRuntimeService>();
        services.AddScoped<IAssistantService, AssistantService>();
        services.AddScoped<IAssistantAttachmentService, AssistantAttachmentService>();
        services.AddScoped<IAssistantGovernanceService, AssistantGovernanceService>();
        return services;
    }
}
