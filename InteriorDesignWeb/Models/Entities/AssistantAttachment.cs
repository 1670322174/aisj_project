using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InteriorDesignWeb.Models.Entities;

public sealed class AssistantAttachment
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long AttachmentID { get; set; }
    public long ConversationID { get; set; }
    public int UserID { get; set; }
    public long? MessageID { get; set; }
    public int? RoomID { get; set; }

    [Required, StringLength(255)]
    public string FileName { get; set; } = string.Empty;
    [Required, StringLength(100)]
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    [Required, StringLength(64)]
    public string Sha256 { get; set; } = string.Empty;
    [Required, StringLength(500)]
    public string CosPath { get; set; } = string.Empty;
    [StringLength(500)]
    public string? ThumbnailPath { get; set; }

    [Required, StringLength(40)]
    public string Kind { get; set; } = "unclassified";
    [Required, StringLength(20)]
    public string VisionStatus { get; set; } = "pending";
    [StringLength(500)]
    public string? VisionError { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }

    public AssistantConversation? Conversation { get; set; }
    public AssistantMessage? Message { get; set; }
    public ProjectRoom? Room { get; set; }
}
