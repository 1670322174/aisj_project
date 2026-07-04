namespace InteriorDesignWeb.Models.DTOs
{
    /*4/6*/
    public class ProjectCreateRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool? IsTemplate { get; set; }
    }
}
