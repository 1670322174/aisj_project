using InteriorDesignWeb.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

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

        var connectionBuilder = new MySqlConnectionStringBuilder(connectionString);
        if (connectionBuilder.Server.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || connectionBuilder.Server.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
        {
            // Local MySQL 5.7 installations commonly expose an incompatible or
            // untrusted TLS endpoint. Loopback traffic does not leave the host.
            connectionBuilder.SslMode = MySqlSslMode.None;
        }

        services.AddDbContext<DesignHubContext>(options =>
        {
            options.UseMySql(
                connectionBuilder.ConnectionString,
                new MySqlServerVersion(new Version(5, 7, 44))
            );
        });

        return services;
    }
}
