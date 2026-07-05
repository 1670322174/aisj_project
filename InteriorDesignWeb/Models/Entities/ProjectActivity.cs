using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InteriorDesignWeb.Models.Entities;

public class ProjectActivity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long ActivityID { get; set; }

    public int ProjectID { get; set; }

    public int? UserID { get; set; }

    [StringLength(50)]
    public string ActivityType { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Title { get; set; }

    [Column(TypeName = "text")]
    public string? Content { get; set; }

    [Column(TypeName = "json")]
    public string? MetadataJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Project Project { get; set; } = null!;

    public User? User { get; set; }
}
