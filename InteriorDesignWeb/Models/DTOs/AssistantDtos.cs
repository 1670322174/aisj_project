using System.ComponentModel.DataAnnotations;

namespace InteriorDesignWeb.Models.DTOs.Assistant;

public sealed class CreateAssistantConversationRequest
{
    [StringLength(120)]
    public string? Title { get; set; }
    public int? ProjectId { get; set; }
    public int? RoomId { get; set; }
}

public sealed class UpdateAssistantBindingRequest
{
    public int? ProjectId { get; set; }
    public int? RoomId { get; set; }
}

public sealed class SendAssistantMessageRequest
{
    [Required, StringLength(4000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;

    [Required, StringLength(64, MinimumLength = 8)]
    public string ClientRequestId { get; set; } = string.Empty;
}

public sealed class AssistantChatRequest
{
    public long ConversationId { get; set; }

    [Required, StringLength(4000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;

    [Required, StringLength(64, MinimumLength = 8)]
    public string ClientRequestId { get; set; } = string.Empty;
}

public sealed class ExecuteAssistantGenerationRequest
{
    [StringLength(64)]
    public string? IdempotencyKey { get; set; }

    [StringLength(8000)]
    public string? Prompt { get; set; }

    [StringLength(4000)]
    public string? NegativePrompt { get; set; }

    public Dictionary<string, object?>? Parameters { get; set; }
    public bool AutoAddToProject { get; set; } = true;
}

public sealed class AssistantBriefDto
{
    public string RoomType { get; set; } = string.Empty;
    public double? Area { get; set; }
    public string Style { get; set; } = string.Empty;
    public List<string> Colors { get; set; } = new();
    public List<string> Materials { get; set; } = new();
    public List<string> Requirements { get; set; } = new();
    public string Lighting { get; set; } = string.Empty;
    public List<string> Constraints { get; set; } = new();
    public List<string> MissingFields { get; set; } = new();
}

public sealed class AssistantGenerationDraftDto
{
    public string GenerationType { get; set; } = "text_to_image";
    public string Prompt { get; set; } = string.Empty;
    public string NegativePrompt { get; set; } = string.Empty;
    public Dictionary<string, object?> Parameters { get; set; } = new();
}

public sealed class AssistantModelOutputDto
{
    public string AssistantText { get; set; } = string.Empty;
    public string Action { get; set; } = "ask_clarification";
    public List<string> MissingFields { get; set; } = new();
    public AssistantBriefDto Brief { get; set; } = new();
    public AssistantGenerationDraftDto? GenerationDraft { get; set; }
}

public sealed class AssistantConversationSummaryDto
{
    public long ConversationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public int? RoomId { get; set; }
    public string? RoomName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class AssistantMessageDto
{
    public long MessageId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? StructuredDataJson { get; set; }
    public string? ModelCode { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class AssistantActionDto
{
    public long ActionId { get; set; }
    public long? MessageId { get; set; }
    public string? JobId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string GenerationType { get; set; } = string.Empty;
    public string? WorkflowCode { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public string? NegativePrompt { get; set; }
    public string ParametersJson { get; set; } = "{}";
    public int? ProjectId { get; set; }
    public int? RoomId { get; set; }
    public bool AutoAddToProject { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class AssistantConversationDetailDto
{
    public AssistantConversationSummaryDto Conversation { get; set; } = new();
    public AssistantBriefDto Brief { get; set; } = new();
    public List<AssistantMessageDto> Messages { get; set; } = new();
    public List<AssistantActionDto> Actions { get; set; } = new();
}

public sealed class AssistantChatResponseDto
{
    public AssistantMessageDto Message { get; set; } = new();
    public AssistantBriefDto Brief { get; set; } = new();
    public AssistantActionDto? ProposedAction { get; set; }
}

public sealed class AssistantGenerationResponseDto
{
    public long ActionId { get; set; }
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string WorkflowCode { get; set; } = string.Empty;
}
