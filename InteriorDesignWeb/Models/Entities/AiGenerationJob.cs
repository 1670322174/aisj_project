using InteriorDesignWeb.Services;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace InteriorDesignWeb.Models.Entities
{
    public class AiGenerationJob
    {
        [Key]  // 主键
        [Required]
        [StringLength(50)]
        public string JobId { get; set; }
        [Required]
        [StringLength(50)]
        public string PromptId { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "processing";

        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string ParametersJson { get; set; }

        // 新增 Progress 属性，用于存储生成进度
        [StringLength(3)]
        public string Progress { get; set; } = "0";  // 默认进度为0%
        [Column(TypeName = "nvarchar(max)")]
        public string? GeneratedImagesJson { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        [NotMapped]
        public FluxGenerationParameters Parameters
        {
            get => JsonConvert.DeserializeObject<FluxGenerationParameters>(ParametersJson)!;
            set => ParametersJson = JsonConvert.SerializeObject(value);
        }

        [NotMapped]
        public List<GeneratedImageInfo> GeneratedImages
        {
            get => string.IsNullOrEmpty(GeneratedImagesJson)
                ? new List<GeneratedImageInfo>()
                : JsonConvert.DeserializeObject<List<GeneratedImageInfo>>(GeneratedImagesJson)!;
            set => GeneratedImagesJson = value == null ? null : JsonConvert.SerializeObject(value);
        }
    }

    public class GeneratedImageInfo
    {
        public string Filename { get; set; }
        public string Type { get; set; }
        public string Subfolder { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
}
