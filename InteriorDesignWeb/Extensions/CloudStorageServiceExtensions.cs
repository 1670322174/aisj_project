using COSXML;
using COSXML.Auth;
using InteriorDesignWeb.Services;

namespace InteriorDesignWeb.Extensions;

public static class CloudStorageServiceExtensions
{
    public static IServiceCollection AddCloudStorage(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var region = configuration["COS:Region"];
        var secretId = configuration["COS:SecretId"];
        var secretKey = configuration["COS:SecretKey"];

        if (string.IsNullOrWhiteSpace(region))
        {
            throw new InvalidOperationException("COS:Region 未配置。");
        }

        if (string.IsNullOrWhiteSpace(secretId) || string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException("COS Secret 配置不完整，请检查 COS:SecretId / COS:SecretKey。");
        }

        services.AddSingleton(provider =>
        {
            var config = new CosXmlConfig.Builder()
                .IsHttps(true)
                .SetRegion(region)
                .SetDebugLog(environment.IsDevelopment())
                .Build();

            var credentialProvider = new DefaultQCloudCredentialProvider(
                secretId,
                secretKey,
                600
            );

            return new CosXmlServer(config, credentialProvider);
        });

        services.AddScoped<CosService>();

        return services;
    }
}
