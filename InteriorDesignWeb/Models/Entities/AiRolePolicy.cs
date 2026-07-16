using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InteriorDesignWeb.Models.Entities;

public sealed class AiRolePolicy
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int RolePolicyID { get; set; }

    public UserRole Role { get; set; }
    public bool AssistantEnabled { get; set; } = true;
    public bool CanProposeGeneration { get; set; } = true;
    public bool CanExecuteGeneration { get; set; } = true;
    public bool CanAutoAddToProject { get; set; } = true;
    public int MaxConcurrentJobs { get; set; } = 1;

    [Column(TypeName = "json")]
    public string AllowedWorkflowCodesJson { get; set; } = "[]";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserID { get; set; }
}
