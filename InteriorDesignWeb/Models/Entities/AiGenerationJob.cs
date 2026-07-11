// 作用：映射 AI 生成任务表，记录用户、工作流、ComfyUI prompt_id、状态、输入、输出和错误信息。
// 本实体不包含旧 Flux 专用参数类型，所有工作流输入统一保存为 JSON。

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InteriorDesignWeb.Models.Entities;

public class AiGenerationJob
{
    [Key]
    [Required]
    [StringLength(50)]
    public string JobId { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string PromptId { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "created";

    [Required]
    [Column(TypeName = "longtext")]
    public string ParametersJson { get; set; } = "{}";

    [Column(TypeName = "longtext")]
    public string? GeneratedImagesJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsAddedToProject { get; set; }

    [StringLength(3)]
    public string Progress { get; set; } = "0";

    public int? UserID { get; set; }

    [StringLength(50)]
    public string WorkflowCode { get; set; } = string.Empty;

    [StringLength(100)]
    public string? ModelCode { get; set; }

    [StringLength(30)]
    public string ProviderType { get; set; } = "ComfyUIServer";

    [StringLength(100)]
    public string? ProviderJobId { get; set; }

    [Column(TypeName = "text")]
    public string? Prompt { get; set; }

    [Column(TypeName = "text")]
    public string? NegativePrompt { get; set; }

    [Column(TypeName = "longtext")]
    public string? InputJson { get; set; }

    [Column(TypeName = "longtext")]
    public string? OutputJson { get; set; }

    [StringLength(50)]
    public string? ErrorCode { get; set; }

    [Column(TypeName = "text")]
    public string? ErrorMessage { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int ProgressValue { get; set; }
    public int CostUnits { get; set; } = 1;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public User? User { get; set; }
    public List<AiGenerationJobImage> Images { get; set; } = new();
}
