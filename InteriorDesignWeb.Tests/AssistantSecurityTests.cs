using InteriorDesignWeb.Controllers;
using InteriorDesignWeb.Models.DTOs.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;

namespace InteriorDesignWeb.Tests;

public sealed class AssistantSecurityTests
{
    [Fact]
    public void AssistantController_RequiresAuthentication()
    {
        Assert.Single(typeof(AssistantController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));
    }

    [Theory]
    [InlineData(nameof(AssistantController.SendMessage))]
    [InlineData(nameof(AssistantController.Chat))]
    public void ChatEndpoints_HaveDedicatedRateLimit(string methodName)
    {
        var method = typeof(AssistantController).GetMethod(methodName);
        Assert.NotNull(method);
        var attribute = Assert.Single(method!.GetCustomAttributes(typeof(EnableRateLimitingAttribute), inherit: true)
            .Cast<EnableRateLimitingAttribute>());
        Assert.Equal("assistant", attribute.PolicyName);
    }

    [Fact]
    public void GenerationRequest_DoesNotAutoAttachByDefault()
    {
        Assert.False(new AIGenerationSubmitRequest().AutoAddToProject);
    }
}
