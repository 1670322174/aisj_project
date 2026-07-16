using System.Text.Json;
using InteriorDesignWeb.Application.Common;
using InteriorDesignWeb.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace InteriorDesignWeb.Tests;

public sealed class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task AppException_ReturnsSafeDiagnosticMetadataAndRequestId()
    {
        var exception = new AppException(
                ErrorCodes.AssistantUnavailable,
                "AI model is temporarily unavailable.",
                StatusCodes.Status502BadGateway)
            .WithDiagnostic(
                "provider_rate_limited",
                "provider_http_response",
                "Retry later.",
                "provider-request-123",
                retryable: true);
        var context = new DefaultHttpContext { TraceIdentifier = "request-123" };
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw exception,
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        var root = document.RootElement;
        Assert.Equal(StatusCodes.Status502BadGateway, context.Response.StatusCode);
        Assert.Equal("request-123", context.Response.Headers["X-Request-ID"]);
        Assert.Equal("request-123", root.GetProperty("requestId").GetString());
        Assert.Equal("provider_rate_limited", root.GetProperty("error").GetProperty("reason").GetString());
        Assert.Equal("provider_http_response", root.GetProperty("error").GetProperty("stage").GetString());
        Assert.Equal("provider-request-123", root.GetProperty("error").GetProperty("upstreamRequestId").GetString());
        Assert.True(root.GetProperty("error").GetProperty("retryable").GetBoolean());
    }

    [Fact]
    public async Task UnhandledException_DoesNotExposeInternalExceptionMessage()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "request-500" };
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("database-password-should-not-leak"),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        var json = document.RootElement.GetRawText();
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.DoesNotContain("database-password-should-not-leak", json);
        Assert.Equal("request-500", document.RootElement.GetProperty("requestId").GetString());
    }
}
