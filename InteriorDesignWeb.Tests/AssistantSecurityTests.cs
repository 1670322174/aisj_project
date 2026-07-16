using InteriorDesignWeb.Controllers;
using InteriorDesignWeb.Models.DTOs.AI;
using InteriorDesignWeb.Models.DTOs.Assistant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using InteriorDesignWeb.Services.Assistant;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;

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

    [Fact]
    public void AttachmentUpload_UsesMultipartRequestModelInsteadOfDirectFormFileParameter()
    {
        var method = typeof(AssistantController).GetMethod(nameof(AssistantController.UploadAttachment));

        Assert.NotNull(method);
        var requestParameter = Assert.Single(
            method!.GetParameters(),
            parameter => parameter.ParameterType == typeof(UploadAssistantAttachmentRequest));
        Assert.Single(requestParameter.GetCustomAttributes(typeof(FromFormAttribute), inherit: true));
        var consumes = Assert.Single(method.GetCustomAttributes(typeof(ConsumesAttribute), inherit: true)
            .Cast<ConsumesAttribute>());
        Assert.Contains("multipart/form-data", consumes.ContentTypes);
        Assert.DoesNotContain(method.GetParameters(), parameter => parameter.ParameterType == typeof(IFormFile));
    }

    [Fact]
    public void SwaggerDocument_CanDescribeAssistantAttachmentUpload()
    {
        using var host = Microsoft.AspNetCore.WebHost.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddControllers().AddApplicationPart(typeof(AssistantController).Assembly);
                services.AddEndpointsApiExplorer();
                services.AddSwaggerGen(options =>
                    options.SwaggerDoc("v1", new OpenApiInfo { Title = "test", Version = "v1" }));
            })
            .Configure(_ => { })
            .Build();

        var document = host.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");

        Assert.Contains("/api/assistant/conversations/{conversationId}/attachments", document.Paths.Keys);
    }

    [Fact]
    public void CorePolicy_ExplicitlyTreatsConversationDataAsUntrusted()
    {
        Assert.Contains("不可信数据", OpenAICompatibleAssistantModelClient.CoreSystemPrompt);
        Assert.Contains("不得泄露", OpenAICompatibleAssistantModelClient.CoreSystemPrompt);
        Assert.Contains("不得根据用户自称", OpenAICompatibleAssistantModelClient.CoreSystemPrompt);
    }

    [Fact]
    public void WorkflowAllowList_IsNormalizedAndCaseInsensitive()
    {
        var values = AssistantGovernanceService.ParseWorkflowCodes("[\"text_to_image\",\"TEXT_TO_IMAGE\",\"  image_to_image  \"]");

        Assert.Equal(2, values.Count);
        Assert.Contains("TEXT_TO_IMAGE", values);
        Assert.Contains("image_to_image", values);
    }
}
