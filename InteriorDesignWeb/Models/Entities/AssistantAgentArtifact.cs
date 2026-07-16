using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InteriorDesignWeb.Models.Entities;

public sealed class AssistantAgentArtifact
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long ArtifactID { get; set; }
    public long ConversationID { get; set; }
    public long RunID { get; set; }
    [Required, StringLength(50)]
    public string AgentID { get; set; } = string.Empty;
    [Required, StringLength(40)]
    public string ArtifactType { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    [Required, StringLength(20)]
    public string Status { get; set; } = "draft";
    [StringLength(160)]
    public string? Title { get; set; }
    [Required, Column(TypeName = "longtext")]
    public string ContentJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AssistantAgentRun? Run { get; set; }
}
