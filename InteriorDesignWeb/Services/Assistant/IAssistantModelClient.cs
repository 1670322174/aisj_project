using InteriorDesignWeb.Models.DTOs.Assistant;

namespace InteriorDesignWeb.Services.Assistant;

public sealed record AssistantModelMessage(string Role, string Content);

public sealed record AssistantModelResult(
    AssistantModelOutputDto Output,
    string Model,
    int InputTokens,
    int OutputTokens,
    int DurationMs);

public interface IAssistantModelClient
{
    Task<AssistantModelResult> CompleteAsync(
        AssistantBriefDto currentBrief,
        IReadOnlyList<AssistantModelMessage> messages,
        CancellationToken cancellationToken = default);
}
