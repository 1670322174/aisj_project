using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InteriorDesignWeb.Models.Entities
{
    /*4/6*/
    public class ProjectImage
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RelationID { get; set; }

        [Required]
        public int ProjectID { get; set; }


        public int? RoomID { get; set; }  // Allow RoomID to be null


        public int? ImageID { get; set; }  // For ImageID (optional)

        // 修改：添加 AiImageID 字段，关联 AiGenerationJobImage
        [ForeignKey("AiGenerationJobImage")]
        public int? AiImageID { get; set; }  // AiImageID 可能为空（对于传统图片）

        [Column(TypeName = "json")]
        public List<string>? CustomTags { get; set; }

        public DateTime AddedTime { get; set; } = DateTime.UtcNow;

        // 导航属性
        public virtual Project? Project { get; set; }
        public virtual ProjectRoom? Room { get; set; }
        public virtual Image? Image { get; set; }

        // 导航属性：关联 AiGenerationJobImage
        public AiGenerationJobImage? AiGenerationJobImage { get; set; }
    }
}
