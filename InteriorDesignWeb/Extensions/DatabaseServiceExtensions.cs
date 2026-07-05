using InteriorDesignWeb.Data;
using Microsoft.EntityFrameworkCore;

namespace InteriorDesignWeb.Extensions;

public static class DatabaseServiceExtensions
{
    public static IServiceCollection AddDatabaseServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DesignDB");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("数据库连接字符串 ConnectionStrings:DesignDB 未配置。");
        }

        services.AddDbContext<DesignHubContext>(options =>
        {
            options.UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(5, 7, 44))
            );
        });

        return services;
    }
}
