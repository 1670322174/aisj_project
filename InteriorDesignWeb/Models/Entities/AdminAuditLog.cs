using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InteriorDesignWeb.Models.Entities;

/// <summary>
/// Immutable audit trail for administrator write operations. Metadata must
/// never contain passwords, token values, secrets or connection strings.
/// </summary>
public sealed class AdminAuditLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long AuditLogID { get; set; }

    public int? AdministratorUserID { get; set; }

    [Required, StringLength(80)]
    public string Action { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string TargetType { get; set; } = string.Empty;

    [StringLength(100)]
    public string? TargetID { get; set; }

    [StringLength(300)]
    public string? Summary { get; set; }

    [Column(TypeName = "json")]
    public string? MetadataJson { get; set; }

    [StringLength(64)]
    public string? IpAddress { get; set; }

    [StringLength(300)]
    public string? UserAgent { get; set; }

    [StringLength(100)]
    public string? RequestID { get; set; }

    public bool Succeeded { get; set; } = true;

    [StringLength(500)]
    public string? FailureReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? Administrator { get; set; }
}
