using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InteriorDesignWeb.Models.Entities;

public class UserQuota
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int QuotaID { get; set; }

    public int UserID { get; set; }

    public int TotalUnits { get; set; }

    public int UsedUnits { get; set; }

    public int RemainingUnits { get; set; }

    public int? MonthlyLimit { get; set; }

    public int MonthlyUsed { get; set; }

    public DateTime? LastResetAt { get; set; }

    public int AssistantTokenLimit5Hours { get; set; }

    public int AssistantTokensUsed5Hours { get; set; }

    public DateTime? AssistantTokenWindowStartedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}
