using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InteriorDesignWeb.Models.Entities;

public sealed class AssistantGenerationAction
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long ActionID { get; set; }

    public long ConversationID { get; set; }
    public long? MessageID { get; set; }

    [StringLength(50)]
    public string? JobID { get; set; }

    public int? ProjectID { get; set; }
    public int? RoomID { get; set; }

    [Required, StringLength(30)]
    public string GenerationType { get; set; } = "text_to_image";

    [StringLength(50)]
    public string? WorkflowCode { get; set; }

    [Required, Column(TypeName = "longtext")]
    public string Prompt { get; set; } = string.Empty;

    [Column(TypeName = "longtext")]
    public string? NegativePrompt { get; set; }

    [Column(TypeName = "longtext")]
    public string ParametersJson { get; set; } = "{}";

    [Required, StringLength(20)]
    public string Status { get; set; } = "proposed";

    [Required, StringLength(64)]
    public string IdempotencyKey { get; set; } = string.Empty;

    public bool AutoAddToProject { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExecutedAt { get; set; }

    [StringLength(500)]
    public string? ErrorMessage { get; set; }

    public AssistantConversation? Conversation { get; set; }
    public AssistantMessage? Message { get; set; }
    public Project? Project { get; set; }
    public ProjectRoom? Room { get; set; }
}
