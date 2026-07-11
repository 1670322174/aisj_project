// 作用：定义 AI 任务查询和结果查询使用的 DTO。
// 任务创建请求统一使用 AIGenerationSubmitRequest，不再保留重复的 CreateAIJobRequest。

namespace InteriorDesignWeb.Models.DTOs.AI;

public class AIJobDto
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? UserID { get; set; }
    public string WorkflowCode { get; set; } = string.Empty;
    public string? ModelCode { get; set; }
    public string ProviderType { get; set; } = string.Empty;
    public string? ProviderJobId { get; set; }
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

public class AIJobResultDto
{
    public int AiImageID { get; set; }
    public string JobId { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? CosPath { get; set; }
    public string? ThumbnailPath { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? OriginalUrl { get; set; }
    public string SourceType { get; set; } = "ai";
    public string? MetadataJson { get; set; }
    public DateTime CreatedAt { get; set; }
}
