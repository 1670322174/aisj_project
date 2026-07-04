using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace InteriorDesignWeb.Models.Entities
{
    public class AiGenerationJobImage
    {
        [Key]
        public int AiImageID { get; set; }

        [ForeignKey("AiGenerationJob")]
        public string JobId { get; set; }  // 外键关联到 AiGenerationJob
        public AiGenerationJob AiGenerationJob { get; set; }  // 导航属性

        public string ImageUrl { get; set; }
        public string CosPath { get; set; }
        public string ThumbnailPath { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsAddedToProject { get; set; }  // 新增字段，标记是否已添加到项目中
    }
}
