using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InteriorDesignWeb.Models.Entities;

public sealed class AssistantPolicyVersion
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long PolicyVersionID { get; set; }

    public int VersionNumber { get; set; }

    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column(TypeName = "text")]
    public string BusinessPrompt { get; set; } = string.Empty;

    public bool IsPublished { get; set; }
    public int CreatedByUserID { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? PublishedByUserID { get; set; }
    public DateTime? PublishedAt { get; set; }
}
