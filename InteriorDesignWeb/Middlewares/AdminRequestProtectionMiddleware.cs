using System.Text.Json;

namespace InteriorDesignWeb.Middlewares;

/// <summary>
/// Requires a non-simple custom header for state-changing administrator calls.
/// A cross-site form cannot set this header, and CORS blocks an untrusted
/// preflight, providing an additional CSRF boundary for cookie authentication.
/// </summary>
public sealed class AdminRequestProtectionMiddleware
{
    public const string HeaderName = "X-DesignHub-Admin";
    private readonly RequestDelegate _next;

    public AdminRequestProtectionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var isAdminApi = context.Request.Path.StartsWithSegments("/api/admin");
        var isSafeMethod = HttpMethods.IsGet(context.Request.Method)
            || HttpMethods.IsHead(context.Request.Method)
            || HttpMethods.IsOptions(context.Request.Method);

        if (isAdminApi
            && !isSafeMethod
            && !string.Equals(
                context.Request.Headers[HeaderName],
                "1",
                StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                success = false,
                code = "ADMIN_REQUEST_HEADER_REQUIRED",
                message = "管理员写操作缺少安全请求标识。",
                requestId = context.TraceIdentifier
            }));
            return;
        }

        await _next(context);
    }
}
