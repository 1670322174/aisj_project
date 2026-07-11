using System.ComponentModel.DataAnnotations;

namespace InteriorDesignWeb.Models.DTOs
{
    public class LoginRequest
    {
        [Required(ErrorMessage = "用户名必填")]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "密码必填")]
        [StringLength(128)]
        public string Password { get; set; } = string.Empty;
    }
}

