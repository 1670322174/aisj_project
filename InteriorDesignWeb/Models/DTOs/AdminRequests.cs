using System.ComponentModel.DataAnnotations;

namespace InteriorDesignWeb.Models.DTOs.Admin;

public sealed class AdminCreateUserRequest
{
    [Required, StringLength(20, MinimumLength = 4)]
    public string Username { get; set; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 10)]
    public string Password { get; set; } = string.Empty;

    [RegularExpression(@"^$|^1[3-9]\d{9}$")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required, StringLength(30)]
    public string Role { get; set; } = "FreeUser";
}

public sealed class AdminChangeRoleRequest
{
    [Required, StringLength(30)]
    public string Role { get; set; } = string.Empty;
}

public sealed class AdminChangeStatusRequest
{
    public bool IsEnabled { get; set; }

    [StringLength(300)]
    public string? Reason { get; set; }
}

public sealed class AdminResetPasswordRequest
{
    [Required, StringLength(128, MinimumLength = 10)]
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class AdminUpdateUserQuotaRequest
{
    [Range(0, int.MaxValue)]
    public int TotalUnits { get; set; }

    [Range(0, int.MaxValue)]
    public int RemainingUnits { get; set; }

    [Range(0, int.MaxValue)]
    public int AssistantTokenLimit5Hours { get; set; }

    [Range(0, int.MaxValue)]
    public int AssistantTokensUsed5Hours { get; set; }

    public bool ResetAssistantWindow { get; set; }
}

public sealed class AdminUpdateGalleryImageRequest
{
    [Required, StringLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string RoomType { get; set; } = string.Empty;

    [StringLength(1500)]
    public string? Tags { get; set; }
}
