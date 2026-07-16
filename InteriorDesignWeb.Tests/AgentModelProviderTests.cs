using System.Net;
using System.Text;
using System.Text.Json;
using InteriorDesignWeb.Config;
using InteriorDesignWeb.Services.Assistant.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace InteriorDesignWeb.Tests;

public sealed class AgentModelProviderTests
{
    [Fact]
    public async Task MiniMaxAnthropic_UsesMessagesProtocolAndParsesToolUse()
    {
        var handler = new CaptureHandler("""
            {
              "id":"msg_1",
              "model":"MiniMax-M3",
              "content":[
                {"type":"text","text":"已判断项目阶段。"},
                {"type":"tool_use","id":"tool_1","name":"emit_ui_action","input":{"kind":"status"}}
              ],
              "usage":{"input_tokens":23,"output_tokens":11}
            }
            """);
        var client = new MiniMaxAnthropicAgentModelClient(
            new StubHttpClientFactory(handler),
            NullLogger<MiniMaxAnthropicAgentModelClient>.Instance);

        var response = await client.CompleteAsync(
            "minimax_frontdesk",
            Profile(AgentModelProviderIds.MiniMax, "anthropic", "https://api.minimaxi.com/anthropic/", "MiniMax-M3"),
            RequestWithTool(),
            CancellationToken.None);

        Assert.Equal("https://api.minimaxi.com/anthropic/v1/messages", handler.RequestUri);
        Assert.Equal("test-key", handler.Headers["x-api-key"]);
        Assert.Contains("已判断项目阶段", response.Content);
        Assert.Single(response.ToolCalls);
        Assert.Equal("status", response.ToolCalls[0].Arguments.GetProperty("kind").GetString());
        Assert.Equal(23, response.InputTokens);
        Assert.Contains("\"input_schema\"", handler.RequestBody);
    }

    [Fact]
    public async Task DeepSeek_UsesChatCompletionsAndParsesFunctionCall()
    {
        var handler = new CaptureHandler("""
            {
              "id":"chat_1",
              "model":"deepseek-v4-flash",
              "choices":[{"message":{"content":"","tool_calls":[{
                "id":"call_1","type":"function","function":{"name":"emit_design_artifact","arguments":"{\"title\":\"日式客厅\"}"}
              }]}}],
              "usage":{"prompt_tokens":31,"completion_tokens":17}
            }
            """);
        var client = new DeepSeekAgentModelClient(
            new StubHttpClientFactory(handler),
            NullLogger<DeepSeekAgentModelClient>.Instance);

        var response = await client.CompleteAsync(
            "deepseek_worker",
            Profile(AgentModelProviderIds.DeepSeek, "openai", "https://api.deepseek.com/", "deepseek-v4-flash"),
            RequestWithTool(AgentModelResponseFormat.JsonObject),
            CancellationToken.None);

        Assert.Equal("https://api.deepseek.com/chat/completions", handler.RequestUri);
        Assert.Equal("Bearer test-key", handler.Authorization);
        Assert.Equal("emit_design_artifact", response.ToolCalls[0].Name);
        Assert.Equal("日式客厅", response.ToolCalls[0].Arguments.GetProperty("title").GetString());
        Assert.Contains("\"response_format\"", handler.RequestBody);
        Assert.Contains("\"reasoning_effort\":\"high\"", handler.RequestBody);
    }

    [Fact]
    public async Task VolcArkResponses_SendsVisionInputAndParsesOutputItems()
    {
        var handler = new CaptureHandler("""
            {
              "id":"resp_1",
              "model":"doubao-seed-2-1-turbo-260628",
              "output":[
                {"type":"message","content":[{"type":"output_text","text":"已识别为毛坯客厅。"}]},
                {"type":"function_call","call_id":"call_vision","name":"emit_visual_artifact","arguments":"{\"roomType\":\"客厅\"}"}
              ],
              "usage":{"input_tokens":45,"output_tokens":19}
            }
            """);
        var client = new VolcArkResponsesAgentModelClient(
            new StubHttpClientFactory(handler),
            NullLogger<VolcArkResponsesAgentModelClient>.Instance);
        var request = new AgentModelRequest(
            "识别室内空间",
            [new AgentModelInputMessage("user", [
                AgentModelContentPart.FromText("分析这张图片"),
                AgentModelContentPart.FromImageUrl("https://example.com/room.jpg", "high")
            ])],
            Tools: [Tool("emit_visual_artifact")]);

        var response = await client.CompleteAsync(
            "volc_vision",
            Profile(AgentModelProviderIds.VolcArk, "responses", "https://ark.cn-beijing.volces.com/api/v3/", "doubao-seed-2-1-turbo-260628"),
            request,
            CancellationToken.None);

        Assert.Equal("https://ark.cn-beijing.volces.com/api/v3/responses", handler.RequestUri);
        Assert.Equal("Bearer test-key", handler.Authorization);
        Assert.Contains("毛坯客厅", response.Content);
        Assert.Equal("客厅", response.ToolCalls[0].Arguments.GetProperty("roomType").GetString());
        Assert.Contains("\"type\":\"input_image\"", handler.RequestBody);
        Assert.Contains("doubao-seed-2-1-turbo-260628", handler.RequestBody);
    }

    private static AgentModelProfileOptions Profile(
        string provider,
        string protocol,
        string baseUrl,
        string model) => new()
        {
            Provider = provider,
            Protocol = protocol,
            BaseUrl = baseUrl,
            ApiKey = "test-key",
            Model = model,
            TimeoutSeconds = 30,
            MaxOutputTokens = 1000,
            ThinkingMode = "enabled",
            ReasoningEffort = "high",
            Capabilities = new AgentModelCapabilitiesOptions
            {
                Text = true,
                Vision = true,
                Tools = true,
                JsonOutput = true,
                Thinking = true
            }
        };

    private static AgentModelRequest RequestWithTool(
        AgentModelResponseFormat responseFormat = AgentModelResponseFormat.Text) => new(
        "协调室内设计任务",
        [AgentModelInputMessage.Text("user", "设计一个日式客厅")],
        Tools: [Tool("emit_ui_action")],
        ResponseFormat: responseFormat);

    private static AgentModelToolDefinition Tool(string name)
    {
        using var document = JsonDocument.Parse("""{"type":"object","properties":{}}""");
        return new AgentModelToolDefinition(name, "测试工具", document.RootElement.Clone());
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class CaptureHandler(string responseJson) : HttpMessageHandler
    {
        public string RequestUri { get; private set; } = string.Empty;
        public string RequestBody { get; private set; } = string.Empty;
        public string Authorization { get; private set; } = string.Empty;
        public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri?.AbsoluteUri ?? string.Empty;
            RequestBody = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Authorization = request.Headers.Authorization?.ToString() ?? string.Empty;
            foreach (var header in request.Headers)
            {
                Headers[header.Key] = string.Join(",", header.Value);
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        }
    }
}
