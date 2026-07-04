using System.ComponentModel.DataAnnotations;

namespace InteriorDesignWeb.Models.DTOs
{
    /*4/6*/
    public class ProjectUpdateRequest
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsTemplate { get; set; }
    }
}
