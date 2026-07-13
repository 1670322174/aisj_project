// 作用：定义统一 AI 生图入口使用的请求、响应和工作流选项 DTO。
// 本文件服务于 /api/ai/generations，不直接依赖具体 ComfyUI Server 节点实现。

namespace InteriorDesignWeb.Models.DTOs.AI;

public class AIGenerationSubmitRequest
{
    /// <summary>
    /// 工作流编码。当前支持 api_banana_image、api_bria_image_edit、api_grok_image_edit、api_luma_image_edit、api_seedance2、api_seedream_image_edit、api_veo3。
    /// </summary>
    public string WorkflowCode { get; set; } = string.Empty;

    /// <summary>
    /// 模型编码。为空时使用工作流默认模型。
    /// </summary>
    public string? ModelCode { get; set; }

    /// <summary>
    /// 正向提示词或图片编辑说明。
    /// </summary>
    public string? Prompt { get; set; }

    /// <summary>
    /// 反向提示词。并非所有工作流都会使用。
    /// </summary>
    public string? NegativePrompt { get; set; }

    /// <summary>
    /// 可选项目 ID。当前不要求生成前选择项目；传入时仅用于任务输入记录。
    /// </summary>
    public int? ProjectId { get; set; }

    /// <summary>
    /// 可选房间 ID。当前不要求生成前选择房间；传入时仅用于任务输入记录。
    /// </summary>
    public int? RoomId { get; set; }

    /// <summary>
    /// When true, successful AI images are attached to ProjectId/RoomId.
    /// Ownership is always verified by the backend.
    /// </summary>
    public bool AutoAddToProject { get; set; }

    /// <summary>
    /// 常用源图文件名。应先通过 /api/ai/generations/upload 上传到 ComfyUI Server 获得。
    /// </summary>
    public string? SourceImageName { get; set; }

    /// <summary>
    /// 常用参考图文件名。应先通过 /api/ai/generations/upload 上传到 ComfyUI Server 获得。
    /// </summary>
    public string? ReferenceImageName { get; set; }

    /// <summary>
    /// 首帧文件名，用于首尾帧视频工作流。
    /// </summary>
    public string? FirstFrameImageName { get; set; }

    /// <summary>
    /// 尾帧文件名，用于首尾帧视频工作流。
    /// </summary>
    public string? LastFrameImageName { get; set; }

    /// <summary>
    /// 通用图片输入映射。key 使用 sourceImage/referenceImage/firstFrame/lastFrame 等字段名，value 是 ComfyUI Server 文件名。
    /// </summary>
    public Dictionary<string, string>? InputImages { get; set; }

    /// <summary>
    /// 工作流扩展参数。可传 width、height、seed、batchSize、duration、resolution、aspectRatio 等。
    /// </summary>
    public Dictionary<string, object?>? Parameters { get; set; }
}

public class AIGenerationSubmitResponse
{
    public string JobId { get; set; } = string.Empty;

    public string ProviderJobId { get; set; } = string.Empty;

    public string PromptId { get; set; } = string.Empty;

    public string WorkflowCode { get; set; } = string.Empty;

    public string? ModelCode { get; set; }

    public string OutputType { get; set; } = "image";

    public string Status { get; set; } = string.Empty;

    public int ProgressValue { get; set; }
}

public class ComfyUploadResponseDto
{
    public string Name { get; set; } = string.Empty;

    public string Subfolder { get; set; } = string.Empty;

    public string Type { get; set; } = "input";

    public string FieldName { get; set; } = string.Empty;
}

public class WorkflowOptionDto
{
    public string WorkflowCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string ProviderType { get; set; } = "ComfyUIServer";

    public string OutputType { get; set; } = "image";

    public string DefaultModelCode { get; set; } = string.Empty;

    public int CostUnits { get; set; }

    public bool Enabled { get; set; }

    public IReadOnlyList<string> RequiredInputs { get; set; } = Array.Empty<string>();

    public IReadOnlyList<string> OptionalInputs { get; set; } = Array.Empty<string>();
}
