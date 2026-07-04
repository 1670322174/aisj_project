using InteriorDesignWeb.Models.Entities;
using System.ComponentModel.DataAnnotations;

namespace InteriorDesignWeb.Models.DTOs
{
    /*4/6*/
    public class ProjectImageAddRequest
    {
        [Required]
        public int? RoomID { get; set; }

        [Required]
        public int? ImageID { get; set; }

        // 新增：支持通过 AiImageID 添加 AI 生成的图片
        public int? AiImageID { get; set; }

        public List<string>? CustomTags { get; set; }

    }
}
