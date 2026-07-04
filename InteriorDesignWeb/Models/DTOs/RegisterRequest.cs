using System.ComponentModel.DataAnnotations;

namespace InteriorDesignWeb.Models.DTOs
{
    public class RegisterRequest
    {
        [Required(ErrorMessage = "用户名必填")]
        [StringLength(20, MinimumLength = 4)]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "密码必填")]
        [StringLength(100, MinimumLength = 6)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "手机号必填")]
        [RegularExpression(@"^1[3-9]\d{9}$", ErrorMessage = "手机号格式不正确")]
        public string PhoneNumber { get; set; } = string.Empty;
    }
}
