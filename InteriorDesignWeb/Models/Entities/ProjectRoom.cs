using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace InteriorDesignWeb.Models.Entities;

public class ProjectRoom
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int RoomID { get; set; }

    [Required]
    public int ProjectID { get; set; }

    public int? ParentRoomID { get; set; }

    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    [StringLength(20)]
    public string? Type { get; set; }

    public int OrderIndex { get; set; }

    [StringLength(50)]
    public string? RoomType { get; set; }

    [StringLength(50)]
    public string? Style { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? Area { get; set; }

    [Column(TypeName = "text")]
    public string? Requirement { get; set; }

    [StringLength(30)]
    public string Status { get; set; } = "not_started";

    public DateTime? UpdatedAt { get; set; }

    [JsonIgnore]
    public Project? Project { get; set; }

    [JsonIgnore]
    public ProjectRoom? ParentRoom { get; set; }

    [JsonIgnore]
    public List<ProjectRoom> Children { get; set; } = new();

    [JsonIgnore]
    public List<ProjectImage> Images { get; set; } = new();
}
