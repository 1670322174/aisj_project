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
            var logLevel = ex.StatusCode >= 500 ? LogLevel.Warning : LogLevel.Information;
            _logger.Log(
                logLevel,
                ex.StatusCode >= 500 ? ex : null,
                "Handled application error. Method={Method}, Path={Path}, StatusCode={StatusCode}, ErrorCode={ErrorCode}, DiagnosticReason={DiagnosticReason}, DiagnosticStage={DiagnosticStage}, Retryable={Retryable}, UpstreamRequestId={UpstreamRequestId}, RequestId={RequestId}",
                context.Request.Method,
                context.Request.Path.Value,
                ex.StatusCode,
                ex.Code,
                ex.DiagnosticReason,
                ex.DiagnosticStage,
                ex.Retryable,
                ex.UpstreamRequestId,
                context.TraceIdentifier);
            await WriteErrorAsync(
                context,
                ex.StatusCode,
                ex.Code,
                ex.Message,
                new ApiErrorDetails
                {
                    Reason = ex.DiagnosticReason,
                    Stage = ex.DiagnosticStage,
                    Hint = ex.DiagnosticHint,
                    UpstreamRequestId = ex.UpstreamRequestId,
                    Retryable = ex.Retryable
                }
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
        string message,
        ApiErrorDetails? error = null)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["X-Request-ID"] = context.TraceIdentifier;

        var response = ApiResponse.Fail(
            code,
            message,
            context.TraceIdentifier,
            error
        );

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
