using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InteriorDesignWeb.Models.Entities;

public class AiGenerationJobImage
{
    [Key]
    public int AiImageID { get; set; }

    [Required]
    [StringLength(50)]
    public string JobId { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public string? CosPath { get; set; }

    public string? ThumbnailPath { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsAddedToProject { get; set; }

    public int? UserID { get; set; }

    [StringLength(50)]
    public string WorkflowCode { get; set; } = "text_to_image";

    [StringLength(100)]
    public string? ModelCode { get; set; }

    [Column(TypeName = "text")]
    public string? Prompt { get; set; }

    [StringLength(30)]
    public string SourceType { get; set; } = "ai";

    [StringLength(50)]
    public string? Style { get; set; }

    [StringLength(50)]
    public string? RoomType { get; set; }

    [StringLength(1000)]
    public string? Tags { get; set; }

    [Column(TypeName = "json")]
    public string? MetadataJson { get; set; }

    public AiGenerationJob AiGenerationJob { get; set; } = null!;

    public User? User { get; set; }
}
