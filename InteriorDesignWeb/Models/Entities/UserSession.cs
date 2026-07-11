using System.ComponentModel.DataAnnotations;

namespace InteriorDesignWeb.Models.Entities;

public sealed class UserSession
{
    public long UserSessionID { get; set; }

    public int UserID { get; set; }

    [Required, StringLength(64)]
    public string TokenHash { get; set; } = string.Empty;

    [StringLength(64)]
    public string? ReplacedByTokenHash { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    [StringLength(300)]
    public string? UserAgent { get; set; }

    [StringLength(64)]
    public string? IpAddress { get; set; }

    public User User { get; set; } = null!;
}
