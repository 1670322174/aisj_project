using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InteriorDesignWeb.Models.Entities;

public sealed class AssistantAgentEvent
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long EventID { get; set; }
    public long RunID { get; set; }
    public int Sequence { get; set; }
    [Required, StringLength(50)]
    public string AgentID { get; set; } = string.Empty;
    [Required, StringLength(40)]
    public string EventType { get; set; } = string.Empty;
    [StringLength(50)]
    public string? Stage { get; set; }
    [Required, StringLength(120)]
    public string Title { get; set; } = string.Empty;
    [StringLength(500)]
    public string? Detail { get; set; }
    [Column(TypeName = "json")]
    public string? DataJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AssistantAgentRun? Run { get; set; }
}
