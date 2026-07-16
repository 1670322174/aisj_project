using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InteriorDesignWeb.Models.Entities;

public sealed class AssistantAgentRun
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long RunID { get; set; }
    public long ConversationID { get; set; }
    public int UserID { get; set; }

    [Required, StringLength(64)]
    public string ClientRequestID { get; set; } = string.Empty;
    [Required, StringLength(20)]
    public string Status { get; set; } = "running";
    [Required, StringLength(50)]
    public string EntryAgentID { get; set; } = "orchestrator";
    [StringLength(50)]
    public string? CurrentAgentID { get; set; }
    [StringLength(50)]
    public string? CurrentStage { get; set; }
    public int ModelCallCount { get; set; }
    public int HandoffCount { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int DurationMs { get; set; }
    [StringLength(50)]
    public string? ErrorCode { get; set; }
    [StringLength(500)]
    public string? ErrorMessage { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public AssistantConversation? Conversation { get; set; }
    public List<AssistantAgentEvent> Events { get; set; } = [];
    public List<AssistantAgentArtifact> Artifacts { get; set; } = [];
}
