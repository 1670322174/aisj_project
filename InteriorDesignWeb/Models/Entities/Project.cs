using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InteriorDesignWeb.Models.Entities
{
    /*4/6*/
    public class Project
    {
        public int ProjectID { get; set; }

        [ForeignKey("User")] // 明确指定导航属性
        public int UserID { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsTemplate { get; set; }

        // 导航属性
        public User User { get; set; } = null!;
        public List<ProjectRoom> Rooms { get; set; } = new();
        public List<ProjectImage> Images { get; set; } = new();
    }
}
