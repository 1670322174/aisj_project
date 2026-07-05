// 作用：定义 AI 生成任务中心使用的数据传输对象。
// 这些 DTO 用于 Controller / Service 之间传递数据，避免直接暴露数据库实体。

namespace InteriorDesignWeb.Models.DTOs.AI;

public class CreateAIJobRequest
{
    public string WorkflowCode { get; set; } = "text_to_image";

    public string? ModelCode { get; set; }

    public string ProviderType { get; set; } = "ComfyUI";

    public string? Prompt { get; set; }

    public string? NegativePrompt { get; set; }

    public Dictionary<string, object?>? Parameters { get; set; }

    public int CostUnits { get; set; } = 1;
}

public class AIJobDto
{
    public string JobId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public int? UserID { get; set; }

    public string WorkflowCode { get; set; } = string.Empty;

    public string? ModelCode { get; set; }

    public string ProviderType { get; set; } = string.Empty;

    public string? Prompt { get; set; }

    public string? NegativePrompt { get; set; }

    public int ProgressValue { get; set; }

    public int CostUnits { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int ImageCount { get; set; }
}
