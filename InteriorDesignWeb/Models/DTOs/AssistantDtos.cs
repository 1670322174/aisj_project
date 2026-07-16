using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

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

public sealed class UploadAssistantAttachmentRequest
{
    [Required]
    public IFormFile File { get; set; } = null!;

    public int? RoomId { get; set; }
}

public sealed class SendAssistantMessageRequest
{
    [Required, StringLength(4000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;

    [Required, StringLength(64, MinimumLength = 8)]
    public string ClientRequestId { get; set; } = string.Empty;

    public List<long> AttachmentIds { get; set; } = [];
}

public sealed class AssistantChatRequest
{
    public long ConversationId { get; set; }

    [Required, StringLength(4000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;

    [Required, StringLength(64, MinimumLength = 8)]
    public string ClientRequestId { get; set; } = string.Empty;

    public List<long> AttachmentIds { get; set; } = [];
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
    [JsonConverter(typeof(AssistantAreaJsonConverter))]
    public string Area { get; set; } = string.Empty;
    public string Style { get; set; } = string.Empty;
    public List<string> Colors { get; set; } = new();
    public List<string> Materials { get; set; } = new();
    public List<string> Requirements { get; set; } = new();
    public string Lighting { get; set; } = string.Empty;
    public List<string> Constraints { get; set; } = new();
    public List<string> MissingFields { get; set; } = new();
}

/// <summary>
/// Models frequently describe an area as "约100㎡" instead of a bare number.
/// Keep that useful qualifier while remaining compatible with older numeric JSON.
/// </summary>
public sealed class AssistantAreaJsonConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString()?.Trim() ?? string.Empty,
            JsonTokenType.Number when reader.TryGetDouble(out var number) =>
                number.ToString("0.##", CultureInfo.InvariantCulture) + "㎡",
            JsonTokenType.Null => string.Empty,
            _ => throw new JsonException("area 必须是文本、数字或 null。")
        };

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value ?? string.Empty);
}

public sealed class AssistantGenerationDraftDto
{
    public string GenerationType { get; set; } = "text_to_image";
    public string WorkflowCode { get; set; } = string.Empty;
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
    public List<AssistantAgentRunDto> AgentRuns { get; set; } = new();
    public List<AssistantAgentArtifactDto> AgentArtifacts { get; set; } = new();
    public List<AssistantAttachmentDto> Attachments { get; set; } = new();
    public List<AssistantRoomProgressDto> Rooms { get; set; } = new();
}

public sealed class AssistantAttachmentDto
{
    public long AttachmentId { get; set; }
    public long? MessageId { get; set; }
    public int? RoomId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string VisionStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class AssistantRoomProgressDto
{
    public int RoomId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RoomType { get; set; } = string.Empty;
    public string Status { get; set; } = "not_started";
    public int OrderIndex { get; set; }
    public bool Selected { get; set; }
}

public sealed class AssistantChatResponseDto
{
    public AssistantMessageDto Message { get; set; } = new();
    public AssistantBriefDto Brief { get; set; } = new();
    public AssistantActionDto? ProposedAction { get; set; }
    public AssistantAgentRunDto? AgentRun { get; set; }
}

public sealed class AssistantGenerationResponseDto
{
    public long ActionId { get; set; }
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string WorkflowCode { get; set; } = string.Empty;
}

public sealed class EvaluateAssistantResultRequest
{
    [Required, StringLength(64, MinimumLength = 8)]
    public string ClientRequestId { get; set; } = string.Empty;
}

public sealed class AssistantResultEvaluationDto
{
    public long ActionId { get; set; }
    public string EvaluationJson { get; set; } = "{}";
    public AssistantAgentRunDto Run { get; set; } = new();
}

public sealed class AssistantAgentEventDto
{
    public long EventId { get; set; }
    public int Sequence { get; set; }
    public string AgentId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? Stage { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string? DataJson { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class AssistantAgentRunDto
{
    public long RunId { get; set; }
    public string ClientRequestId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string EntryAgentId { get; set; } = string.Empty;
    public string? CurrentAgentId { get; set; }
    public string? CurrentStage { get; set; }
    public int ModelCallCount { get; set; }
    public int HandoffCount { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int DurationMs { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<AssistantAgentEventDto> Events { get; set; } = [];
}

public sealed class AssistantAgentArtifactDto
{
    public long ArtifactId { get; set; }
    public long RunId { get; set; }
    public string AgentId { get; set; } = string.Empty;
    public string ArtifactType { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string ContentJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
}
