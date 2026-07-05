using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InteriorDesignWeb.Models.Entities;

public class Project
{
    public int ProjectID { get; set; }

    [ForeignKey("User")]
    public int UserID { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsTemplate { get; set; }

    [StringLength(30)]
    public string Status { get; set; } = "draft";

    public DateTime? UpdatedAt { get; set; }

    public int? CoverImageID { get; set; }

    public int? CoverAiImageID { get; set; }

    [StringLength(50)]
    public string? Style { get; set; }

    [StringLength(50)]
    public string? HouseType { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? Area { get; set; }

    [StringLength(1000)]
    public string? Tags { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public User User { get; set; } = null!;

    public Image? CoverImage { get; set; }

    public AiGenerationJobImage? CoverAiImage { get; set; }

    public List<ProjectRoom> Rooms { get; set; } = new();

    public List<ProjectImage> Images { get; set; } = new();
}
