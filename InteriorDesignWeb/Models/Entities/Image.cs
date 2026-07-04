using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InteriorDesignWeb.Models.Entities
{
    public class Image
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ImageID { get; set; }

        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [StringLength(512)]
        public string FilePath { get; set; } = string.Empty;

        [Required]
        [StringLength(512)]
        public string ThumbnailPath { get; set; } = string.Empty; // 缩略图路径

        [Required]
        public int FileSize { get; set; } // 单位：字节

        [Required]
        [StringLength(64)]
        public string FileHash { get; set; } = string.Empty;

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime UploadTime { get; set; } = DateTime.Now;

        [StringLength(1500)] // 适当增加长度
        public string? Tags { get; set; }

        // 4/6新增房间类型字段
        [Required]
        [StringLength(50)]
        public string? Room { get; set; }
    }
}
