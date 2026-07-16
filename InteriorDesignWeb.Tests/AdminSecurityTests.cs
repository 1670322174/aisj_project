using InteriorDesignWeb.Controllers;
using InteriorDesignWeb.Middlewares;
using InteriorDesignWeb.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace InteriorDesignWeb.Tests;

public sealed class AdminSecurityTests
{
    [Fact]
    public void AdminController_RequiresAdministratorRole()
    {
        var authorize = Assert.Single(
            typeof(AdminController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>());

        Assert.Equal(nameof(UserRole.Administrator), authorize.Roles);
    }

    [Fact]
    public void AdminAiGovernanceController_RequiresAdministratorRole()
    {
        var authorize = Assert.Single(
            typeof(AdminAIGovernanceController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>());

        Assert.Equal(nameof(UserRole.Administrator), authorize.Roles);
    }

    [Fact]
    public async Task UnsafeAdminRequest_WithoutProtectionHeader_IsRejected()
    {
        var nextCalled = false;
        var middleware = new AdminRequestProtectionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateContext(HttpMethods.Post, "/api/admin/users");

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(nextCalled);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        Assert.Contains("ADMIN_REQUEST_HEADER_REQUIRED", await reader.ReadToEndAsync());
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public async Task SafeAdminRequest_DoesNotRequireProtectionHeader(string method)
    {
        var nextCalled = false;
        var middleware = new AdminRequestProtectionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateContext(method, "/api/admin/overview");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task UnsafeAdminRequest_WithProtectionHeader_ContinuesPipeline()
    {
        var nextCalled = false;
        var middleware = new AdminRequestProtectionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateContext(HttpMethods.Delete, "/api/admin/users/7/sessions");
        context.Request.Headers[AdminRequestProtectionMiddleware.HeaderName] = "1";

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task NonAdminApiRequest_DoesNotRequireProtectionHeader()
    {
        var nextCalled = false;
        var middleware = new AdminRequestProtectionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateContext(HttpMethods.Post, "/api/ai/jobs");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    private static DefaultHttpContext CreateContext(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }
}
