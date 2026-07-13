using InteriorDesignWeb.Models.DTOs.Assistant;

namespace InteriorDesignWeb.Services.Assistant;

public interface IAssistantService
{
    Task<AssistantConversationDetailDto> CreateConversationAsync(int userId, CreateAssistantConversationRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<AssistantConversationSummaryDto>> GetConversationsAsync(int userId, CancellationToken cancellationToken);
    Task<AssistantConversationDetailDto> GetConversationAsync(int userId, long conversationId, CancellationToken cancellationToken);
    Task<AssistantConversationSummaryDto> UpdateBindingAsync(int userId, long conversationId, UpdateAssistantBindingRequest request, CancellationToken cancellationToken);
    Task<AssistantChatResponseDto> SendMessageAsync(int userId, long conversationId, SendAssistantMessageRequest request, CancellationToken cancellationToken);
    Task<AssistantGenerationResponseDto> ExecuteGenerationAsync(int userId, long conversationId, long actionId, ExecuteAssistantGenerationRequest request, CancellationToken cancellationToken);
    Task DeleteConversationAsync(int userId, long conversationId, CancellationToken cancellationToken);
}
