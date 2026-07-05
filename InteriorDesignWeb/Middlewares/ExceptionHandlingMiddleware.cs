using System.Text.Json;
using InteriorDesignWeb.Application.Common;

namespace InteriorDesignWeb.Middlewares;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            await WriteErrorAsync(
                context,
                ex.StatusCode,
                ex.Code,
                ex.Message
            );
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access. Path={Path}", context.Request.Path);

            await WriteErrorAsync(
                context,
                StatusCodes.Status401Unauthorized,
                ErrorCodes.Unauthorized,
                "未登录或登录已过期"
            );
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Resource not found. Path={Path}", context.Request.Path);

            await WriteErrorAsync(
                context,
                StatusCodes.Status404NotFound,
                ErrorCodes.NotFound,
                ex.Message
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unhandled exception. Path={Path}, RequestId={RequestId}",
                context.Request.Path,
                context.TraceIdentifier
            );

            await WriteErrorAsync(
                context,
                StatusCodes.Status500InternalServerError,
                ErrorCodes.ServerError,
                "服务器发生错误，请稍后重试"
            );
        }
    }

    private static async Task WriteErrorAsync(
        HttpContext context,
        int statusCode,
        string code,
        string message)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        var response = ApiResponse.Fail(
            code,
            message,
            context.TraceIdentifier
        );

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
