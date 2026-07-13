using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InteriorDesignWeb.Models.Entities;

public sealed class AssistantMessage
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long MessageID { get; set; }

    public long ConversationID { get; set; }

    [Required, StringLength(20)]
    public string Role { get; set; } = "user";

    [Required, Column(TypeName = "longtext")]
    public string Content { get; set; } = string.Empty;

    [Column(TypeName = "json")]
    public string? StructuredDataJson { get; set; }

    [StringLength(100)]
    public string? ModelCode { get; set; }

    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int DurationMs { get; set; }

    [StringLength(64)]
    public string? ClientRequestID { get; set; }

    [StringLength(500)]
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AssistantConversation? Conversation { get; set; }
}
