using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace InteriorDesignWeb.Models.DTOs
{
    // 请求模型调整
    public class ImageSearchRequest
    {
        [FromQuery(Name = "keyword")] // 显式指定参数别名
        public string? Keyword { get; set; }

        [FromQuery]
        [Range(0, int.MaxValue)]
        public int MinSizeKB { get; set; } = 0;

        [FromQuery]
        [Range(0, int.MaxValue)]
        public int MaxSizeKB { get; set; } = int.MaxValue;

        [FromQuery]
        public DateTime? StartDate { get; set; }

        [FromQuery]
        public DateTime? EndDate { get; set; }

        [FromQuery]
        [Range(1, 100)]
        public int Page { get; set; } = 1;

        [FromQuery]
        [Range(1, 200)]
        public int PageSize { get; set; } = 20;


        [FromQuery(Name = "seed")]
        public string? Seed { get; set; }               // 可选：洗牌用；不传则后端生成稳定种子

        [FromQuery(Name = "pageToken")]
        public string? PageToken { get; set; }          // 光标分页用（下一页/上一页）

        // 兼容历史/别名（可选）
        [FromQuery(Name = "q")] public string? Q { get; set; }
    }
}
