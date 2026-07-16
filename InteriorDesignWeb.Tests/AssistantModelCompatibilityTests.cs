using System.Net;
using System.Text;
using System.Text.Json;
using InteriorDesignWeb.Config;
using InteriorDesignWeb.Models.DTOs.Assistant;
using InteriorDesignWeb.Services.Assistant;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InteriorDesignWeb.Tests;

public sealed class AssistantModelCompatibilityTests
{
    [Fact]
    public async Task StructuredOutput_AcceptsHumanReadableAreaWithoutFallingBack()
    {
        var output = JsonSerializer.Serialize(new
        {
            assistantText = "我建议采用原木与米白的日式会客空间，方案已准备好。",
            action = "propose_generation",
            missingFields = Array.Empty<string>(),
            brief = new
            {
                roomType = "客厅",
                area = "约100㎡",
                style = "日式",
                colors = new[] { "原木", "米白" },
                materials = new[] { "原木", "障子纸" },
                requirements = new[] { "榻榻米会客区" },
                lighting = "自然光与暖白间接照明",
                constraints = Array.Empty<string>(),
                missingFields = Array.Empty<string>()
            },
            generationDraft = new
            {
                generationType = "text_to_image",
                prompt = "Japanese living room, natural wood, warm daylight",
                negativePrompt = "low quality",
                parameters = new { aspectRatio = "16:9" }
            }
        });
        var handler = new QueueHandler(Completion(output, "MiniMax-M2.7"));
        var client = CreateClient(handler, repair: true, fallback: true);

        var result = await client.CompleteAsync(
            new AssistantBriefDto(),
            [new AssistantModelMessage("user", "约100平的日式客厅")],
            "主动提出设计方案",
            CancellationToken.None);

        Assert.Equal("propose_generation", result.Output.Action);
        Assert.Equal("约100㎡", result.Output.Brief.Area);
        Assert.NotNull(result.Output.GenerationDraft);
        Assert.Equal("structured_primary", result.OutputMode);
        Assert.Single(handler.RequestBodies);
    }

    [Fact]
    public async Task MiniMaxAutoMode_RepairsNaturalLanguageIntoStructuredOutput()
    {
        var validOutput = JsonSerializer.Serialize(new
        {
            assistantText = "请补充客厅面积。",
            action = "ask_clarification",
            missingFields = new[] { "area" },
            brief = new
            {
                roomType = "客厅",
                area = (double?)null,
                style = "现代",
                colors = Array.Empty<string>(),
                materials = Array.Empty<string>(),
                requirements = Array.Empty<string>(),
                lighting = "",
                constraints = Array.Empty<string>(),
                missingFields = new[] { "area" }
            },
            generationDraft = (object?)null
        });
        var handler = new QueueHandler(
            Completion("我会先了解你的设计需求。", "MiniMax-M2.7"),
            Completion(validOutput, "MiniMax-M2.7"));
        var client = CreateClient(handler, repair: true, fallback: true);

        var result = await client.CompleteAsync(
            new AssistantBriefDto(),
            [new AssistantModelMessage("user", "现代客厅")],
            "专业室内设计顾问",
            CancellationToken.None);

        Assert.Equal("ask_clarification", result.Output.Action);
        Assert.Equal("请补充客厅面积。", result.Output.AssistantText);
        Assert.Equal(2, handler.RequestBodies.Count);
        Assert.All(handler.RequestBodies, body => Assert.DoesNotContain("response_format", body));
        Assert.Equal(20, result.InputTokens);
        Assert.Equal(10, result.OutputTokens);
        Assert.Equal("structured_repair", result.OutputMode);
    }

    [Fact]
    public async Task NaturalLanguageFallback_IsDisplayOnlyAndCannotGenerate()
    {
        var handler = new QueueHandler(Completion("<think>内部推理</think>\n这是普通中文回复。", "MiniMax-M2.7"));
        var client = CreateClient(handler, repair: false, fallback: true);

        var result = await client.CompleteAsync(
            new AssistantBriefDto { RoomType = "卧室" },
            [new AssistantModelMessage("user", "继续")],
            "专业室内设计顾问",
            CancellationToken.None);

        Assert.Equal("update_brief", result.Output.Action);
        Assert.Null(result.Output.GenerationDraft);
        Assert.Equal("这是普通中文回复。", result.Output.AssistantText);
        Assert.Equal("卧室", result.Output.Brief.RoomType);
        Assert.Equal("natural_language_fallback", result.OutputMode);
    }

    private static OpenAICompatibleAssistantModelClient CreateClient(
        HttpMessageHandler handler,
        bool repair,
        bool fallback)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.minimaxi.com/v1/") };
        return new OpenAICompatibleAssistantModelClient(
            httpClient,
            Options.Create(new AssistantOptions
            {
                Enabled = true,
                BaseUrl = "https://api.minimaxi.com/v1",
                ApiKey = "test-key",
                Model = "MiniMax-M2.7",
                UseJsonResponseFormat = true,
                ResponseFormatMode = "auto",
                RepairInvalidStructuredOutput = repair,
                AllowNaturalLanguageFallback = fallback
            }),
            NullLogger<OpenAICompatibleAssistantModelClient>.Instance);
    }

    private static HttpResponseMessage Completion(string content, string model)
    {
        var json = JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content } } },
            model,
            usage = new { prompt_tokens = 10, completion_tokens = 5 }
        });
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class QueueHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;
        public List<string> RequestBodies { get; } = new();

        public QueueHandler(params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return _responses.Dequeue();
        }
    }
}
