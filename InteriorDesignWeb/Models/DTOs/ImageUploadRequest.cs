namespace InteriorDesignWeb.Models.DTOs
{
    public class ImageUploadRequest
    {
        public IFormFile? file { get; set; }
        public string? houseType { get; set; }
        public string? roomType { get; set; }
        public string? style { get; set; }
        public string? material { get; set; }
        public string? elements { get; set; }
        public string? other { get; set; }
    }
}
