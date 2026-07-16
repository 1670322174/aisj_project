using System.Text.Json;
using InteriorDesignWeb.Config;

namespace InteriorDesignWeb.Services.Assistant.Models;

public enum AgentModelResponseFormat
{
    Text,
    JsonObject
}

public enum AgentThinkingMode
{
    Disabled,
    Enabled,
    Adaptive
}

public sealed record AgentModelContentPart(
    string Type,
    string? Text = null,
    string? ImageUrl = null,
    string? Detail = null)
{
    public static AgentModelContentPart FromText(string text) => new("text", Text: text);
    public static AgentModelContentPart FromImageUrl(string imageUrl, string detail = "auto") =>
        new("image_url", ImageUrl: imageUrl, Detail: detail);
}

public sealed record AgentModelInputMessage(
    string Role,
    IReadOnlyList<AgentModelContentPart> Content)
{
    public static AgentModelInputMessage Text(string role, string text) =>
        new(role, [AgentModelContentPart.FromText(text)]);
}

public sealed record AgentModelToolDefinition(
    string Name,
    string Description,
    JsonElement Parameters);

public sealed record AgentModelToolCall(
    string Id,
    string Name,
    JsonElement Arguments);

public sealed record AgentModelRequest(
    string SystemPrompt,
    IReadOnlyList<AgentModelInputMessage> Messages,
    IReadOnlyList<AgentModelToolDefinition>? Tools = null,
    string? ForcedToolName = null,
    AgentModelResponseFormat ResponseFormat = AgentModelResponseFormat.Text,
    AgentThinkingMode? ThinkingMode = null,
    string? ReasoningEffort = null,
    double? Temperature = null,
    int? MaxOutputTokens = null);

public sealed record AgentModelResponse(
    string ProfileId,
    string Provider,
    string Model,
    string Content,
    IReadOnlyList<AgentModelToolCall> ToolCalls,
    int InputTokens,
    int OutputTokens,
    int DurationMs,
    string? ProviderRequestId,
    string? ProviderResponseId,
    string? ContinuationStateJson);

public sealed record AgentModelProfileStatus(
    string ProfileId,
    bool Enabled,
    bool Configured,
    string Provider,
    string Protocol,
    string BaseUrl,
    string Model,
    bool ApiKeyConfigured,
    int TimeoutSeconds,
    int MaxOutputTokens,
    string ThinkingMode,
    string ReasoningEffort,
    AgentModelCapabilitiesOptions Capabilities,
    IReadOnlyList<string> ValidationErrors);

public interface IAgentModelProviderClient
{
    string ProviderId { get; }

    Task<AgentModelResponse> CompleteAsync(
        string profileId,
        AgentModelProfileOptions profile,
        AgentModelRequest request,
        CancellationToken cancellationToken = default);
}

public interface IAgentModelRouter
{
    Task<AgentModelResponse> CompleteAsync(
        string profileId,
        AgentModelRequest request,
        CancellationToken cancellationToken = default);

    IReadOnlyList<AgentModelProfileStatus> GetProfileStatuses();
    AgentModelProfileStatus GetProfileStatus(string profileId);
}
