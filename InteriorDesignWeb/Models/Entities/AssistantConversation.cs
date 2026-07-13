using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InteriorDesignWeb.Models.Entities;

public sealed class AssistantConversation
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long ConversationID { get; set; }

    public int UserID { get; set; }
    public int? ProjectID { get; set; }
    public int? RoomID { get; set; }

    [Required, StringLength(120)]
    public string Title { get; set; } = "新设计对话";

    [Required, StringLength(20)]
    public string Status { get; set; } = "active";

    [Column(TypeName = "json")]
    public string? CurrentBriefJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public User? User { get; set; }
    public Project? Project { get; set; }
    public ProjectRoom? Room { get; set; }
    public List<AssistantMessage> Messages { get; set; } = new();
    public List<AssistantGenerationAction> GenerationActions { get; set; } = new();
}
