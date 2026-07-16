using System.ComponentModel.DataAnnotations;

namespace InteriorDesignWeb.Models.DTOs.Admin;

public sealed class AdminCreateAssistantPolicyDraftRequest
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(12000, MinimumLength = 20)]
    public string BusinessPrompt { get; set; } = string.Empty;
}

public sealed class AdminUpdateAiRolePolicyRequest
{
    public bool AssistantEnabled { get; set; }
    public bool CanProposeGeneration { get; set; }
    public bool CanExecuteGeneration { get; set; }
    public bool CanAutoAddToProject { get; set; }

    [Range(1, 100)]
    public int MaxConcurrentJobs { get; set; } = 1;

    public List<string> AllowedWorkflowCodes { get; set; } = new();
}

public sealed class AdminUpdateAiUserOverrideRequest
{
    public bool? AssistantEnabled { get; set; }
    public bool? CanProposeGeneration { get; set; }
    public bool? CanExecuteGeneration { get; set; }
    public bool? CanAutoAddToProject { get; set; }

    [Range(1, 100)]
    public int? MaxConcurrentJobs { get; set; }

    public List<string>? AllowedWorkflowCodes { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
