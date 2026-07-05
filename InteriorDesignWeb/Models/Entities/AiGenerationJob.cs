using InteriorDesignWeb.Services;
using Newtonsoft.Json;
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
    public string Status { get; set; } = "processing";

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
    public string WorkflowCode { get; set; } = "text_to_image";

    [StringLength(100)]
    public string? ModelCode { get; set; }

    [StringLength(30)]
    public string ProviderType { get; set; } = "ComfyUI";

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

    public User? User { get; set; }

    public List<AiGenerationJobImage> Images { get; set; } = new();

    [NotMapped]
    public FluxGenerationParameters Parameters
    {
        get => JsonConvert.DeserializeObject<FluxGenerationParameters>(ParametersJson)!;
        set => ParametersJson = JsonConvert.SerializeObject(value);
    }

    [NotMapped]
    public List<GeneratedImageInfo> GeneratedImages
    {
        get => string.IsNullOrEmpty(GeneratedImagesJson)
            ? new List<GeneratedImageInfo>()
            : JsonConvert.DeserializeObject<List<GeneratedImageInfo>>(GeneratedImagesJson)!;
        set => GeneratedImagesJson = value == null ? null : JsonConvert.SerializeObject(value);
    }
}

public class GeneratedImageInfo
{
    public string Filename { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Subfolder { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
}
