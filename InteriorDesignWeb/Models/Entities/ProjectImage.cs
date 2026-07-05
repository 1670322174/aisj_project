using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InteriorDesignWeb.Models.Entities;

public class ProjectImage
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int RelationID { get; set; }

    [Required]
    public int ProjectID { get; set; }

    public int? RoomID { get; set; }

    public int? ImageID { get; set; }

    public int? AiImageID { get; set; }

    [Column(TypeName = "json")]
    public List<string>? CustomTags { get; set; }

    public DateTime AddedTime { get; set; } = DateTime.UtcNow;

    [StringLength(30)]
    public string SourceType { get; set; } = "gallery";

    public int SortOrder { get; set; }

    public bool IsFavorite { get; set; }

    public bool IsCover { get; set; }

    [Column(TypeName = "text")]
    public string? Note { get; set; }

    public int? CreatedByUserID { get; set; }

    public virtual Project? Project { get; set; }

    public virtual ProjectRoom? Room { get; set; }

    public virtual Image? Image { get; set; }

    public AiGenerationJobImage? AiGenerationJobImage { get; set; }

    public User? CreatedByUser { get; set; }
}
