using InteriorDesignWeb.Config;
using InteriorDesignWeb.Services.Assistant;

namespace InteriorDesignWeb.Extensions;

public static class AssistantServiceExtensions
{
    public static IServiceCollection AddAssistantInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(AssistantOptions.SectionName);
        services.Configure<AssistantOptions>(section);
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
        services.AddScoped<IAssistantService, AssistantService>();
        return services;
    }
}
