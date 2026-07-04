using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace InteriorDesignWeb.Models.Entities
{
    /*4/6*/
    public class ProjectRoom
    {
        [Key] // ← 添加Key特性
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RoomID { get; set; }

        [Required]
        public int ProjectID { get; set; }

        public int? ParentRoomID { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        [StringLength(20)]
        public string? Type { get; set; } // 新增字段Type

        public int OrderIndex { get; set; }

        // 导航属性
        [JsonIgnore]
        public Project? Project { get; set; }
        [JsonIgnore]
        public ProjectRoom? ParentRoom { get; set; }
        [JsonIgnore]
        public List<ProjectRoom> Children { get; set; } = new();
        [JsonIgnore]
        public List<ProjectImage> Images { get; set; } = new();
    }
}
