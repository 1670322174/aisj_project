// 作用：描述一个可由 ComfyUI Server 执行的工作流及其输入映射。
// 通过配置化节点映射接入 7 个工作流，避免在 Controller 或旧服务中写 7 套 if/else。

namespace InteriorDesignWeb.Services.AI;

public class WorkflowDefinition
{
    public string WorkflowCode { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string ProviderType { get; init; } = "ComfyUIServer";

    public string WorkflowFilePath { get; init; } = string.Empty;

    public string OutputType { get; init; } = "image";

    public string DefaultModelCode { get; init; } = string.Empty;

    public int CostUnits { get; init; } = 1;

    public bool Enabled { get; init; } = true;

    public IReadOnlyList<WorkflowInputMapping> InputMappings { get; init; } = Array.Empty<WorkflowInputMapping>();

    public IReadOnlyList<string> OutputNodeIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> RequiredInputs => InputMappings
        .Where(mapping => mapping.Required)
        .Select(mapping => mapping.Field)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public IReadOnlyList<string> OptionalInputs => InputMappings
        .Where(mapping => !mapping.Required)
        .Select(mapping => mapping.Field)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

public class WorkflowInputMapping
{
    public string Field { get; init; } = string.Empty;

    public string NodeId { get; init; } = string.Empty;

    public string InputKey { get; init; } = string.Empty;

    public bool Required { get; init; }

    public string? DefaultValue { get; init; }
}
