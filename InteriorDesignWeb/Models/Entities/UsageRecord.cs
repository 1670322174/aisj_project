using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InteriorDesignWeb.Models.Entities;

public class UsageRecord
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long UsageID { get; set; }

    public int? UserID { get; set; }

    [StringLength(50)]
    public string? JobId { get; set; }

    [StringLength(50)]
    public string UsageType { get; set; } = "ai_generation";

    [StringLength(50)]
    public string? WorkflowCode { get; set; }

    [StringLength(100)]
    public string? ModelCode { get; set; }

    [StringLength(30)]
    public string? ProviderType { get; set; } = "ComfyUI";

    public int Units { get; set; } = 1;

    [StringLength(30)]
    public string Status { get; set; } = "created";

    [Column(TypeName = "json")]
    public string? RequestJson { get; set; }

    [Column(TypeName = "json")]
    public string? ResultJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }

    public AiGenerationJob? AiGenerationJob { get; set; }
}
