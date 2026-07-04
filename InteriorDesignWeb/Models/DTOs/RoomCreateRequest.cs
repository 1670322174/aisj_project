using System.ComponentModel.DataAnnotations;

namespace InteriorDesignWeb.Models.DTOs
{
    /*4/6*/
    public class RoomCreateRequest
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        [Required]
        [StringLength(20)]
        public string? Type { get; set; } // bedroom/living_room/bathroom/balcony
        
        public int? ParentRoomID { get; set; }
    }
}
