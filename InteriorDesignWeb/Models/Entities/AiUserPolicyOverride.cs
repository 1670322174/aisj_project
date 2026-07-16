using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InteriorDesignWeb.Models.Entities;

public sealed class AiUserPolicyOverride
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long UserPolicyOverrideID { get; set; }

    public int UserID { get; set; }
    public bool? AssistantEnabled { get; set; }
    public bool? CanProposeGeneration { get; set; }
    public bool? CanExecuteGeneration { get; set; }
    public bool? CanAutoAddToProject { get; set; }
    public int? MaxConcurrentJobs { get; set; }

    [Column(TypeName = "json")]
    public string? AllowedWorkflowCodesJson { get; set; }

    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public int UpdatedByUserID { get; set; }
}
