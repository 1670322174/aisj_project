using InteriorDesignWeb.Models.DTOs.Assistant;

namespace InteriorDesignWeb.Services.Assistant;

public sealed record AssistantModelMessage(string Role, string Content);

public sealed record AssistantModelResult(
    AssistantModelOutputDto Output,
    string Model,
    int InputTokens,
    int OutputTokens,
    int DurationMs,
    string OutputMode,
    string? ProviderRequestId);

public interface IAssistantModelClient
{
    Task<AssistantModelResult> CompleteAsync(
        AssistantBriefDto currentBrief,
        IReadOnlyList<AssistantModelMessage> messages,
        string businessPrompt,
        CancellationToken cancellationToken = default);
}
