// 作用：注册当前项目 workflow 目录中的 7 个 ComfyUI API 格式工作流。
// 本类只维护工作流元数据和节点映射，不负责调用 ComfyUI Server 或保存结果。

using InteriorDesignWeb.Application.Common;
using Microsoft.AspNetCore.Http;

namespace InteriorDesignWeb.Services.AI;

public class WorkflowRegistry : IWorkflowRegistry
{
    private readonly IReadOnlyDictionary<string, WorkflowDefinition> _workflows;

    public WorkflowRegistry(IWebHostEnvironment environment)
    {
        var root = environment.ContentRootPath;
        var workflowRoot = Path.Combine(root, "workflow");

        var definitions = new[]
        {
            new WorkflowDefinition
            {
                WorkflowCode = "api_banana_image",
                Name = "高级参考图编辑",
                Description = "上传原图和参考图，进行风格迁移、材质迁移或家居替换。",
                WorkflowFilePath = Path.Combine(workflowRoot, "api_banana_image.json"),
                OutputType = "image",
                DefaultModelCode = "Nano Banana 2 (Gemini 3.1 Flash Image)",
                CostUnits = 21,
                OutputNodeIds = new[] { "9" },
                InputMappings = new[]
                {
                    new WorkflowInputMapping { Field = "sourceImage", NodeId = "16", InputKey = "image", Required = true },
                    new WorkflowInputMapping { Field = "referenceImage", NodeId = "24", InputKey = "image", Required = true },
                    new WorkflowInputMapping { Field = "prompt", NodeId = "23", InputKey = "prompt", Required = true },
                    new WorkflowInputMapping { Field = "seed", NodeId = "23", InputKey = "seed" },
                    new WorkflowInputMapping { Field = "resolution", NodeId = "23", InputKey = "resolution" },
                    new WorkflowInputMapping { Field = "aspectRatio", NodeId = "23", InputKey = "aspect_ratio" }
                }
            },
            new WorkflowDefinition
            {
                WorkflowCode = "api_bria_image_edit",
                Name = "文本图片编辑",
                Description = "使用文字对图片进行编辑，例如去水印、去杂物、换灯光、换材质。",
                WorkflowFilePath = Path.Combine(workflowRoot, "api_bria_image_edit.json"),
                OutputType = "image",
                DefaultModelCode = "FIBO",
                CostUnits = 9,
                OutputNodeIds = new[] { "8" },
                InputMappings = new[]
                {
                    new WorkflowInputMapping { Field = "sourceImage", NodeId = "5", InputKey = "image", Required = true },
                    new WorkflowInputMapping { Field = "prompt", NodeId = "3", InputKey = "prompt", Required = true },
                    new WorkflowInputMapping { Field = "negativePrompt", NodeId = "3", InputKey = "negative_prompt" },
                    new WorkflowInputMapping { Field = "seed", NodeId = "3", InputKey = "seed" },
                    new WorkflowInputMapping { Field = "steps", NodeId = "3", InputKey = "steps" },
                    new WorkflowInputMapping { Field = "guidanceScale", NodeId = "3", InputKey = "guidance_scale" }
                }
            },
            new WorkflowDefinition
            {
                WorkflowCode = "api_grok_image_edit",
                Name = "简单文生图",
                Description = "根据提示词直接生成图片。",
                WorkflowFilePath = Path.Combine(workflowRoot, "api_grok_image_edit.json"),
                OutputType = "image",
                DefaultModelCode = "grok-imagine-image",
                CostUnits = 4,
                OutputNodeIds = new[] { "3" },
                InputMappings = new[]
                {
                    new WorkflowInputMapping { Field = "prompt", NodeId = "2", InputKey = "prompt", Required = true },
                    new WorkflowInputMapping { Field = "batchSize", NodeId = "2", InputKey = "number_of_images" },
                    new WorkflowInputMapping { Field = "seed", NodeId = "2", InputKey = "seed" },
                    new WorkflowInputMapping { Field = "resolution", NodeId = "2", InputKey = "resolution" },
                    new WorkflowInputMapping { Field = "aspectRatio", NodeId = "2", InputKey = "aspect_ratio" }
                }
            },
            new WorkflowDefinition
            {
                WorkflowCode = "api_luma_image_edit",
                Name = "高级文本图像编辑",
                Description = "通过文字对室内图片做高级编辑，例如夜景、灯光氛围转换。",
                WorkflowFilePath = Path.Combine(workflowRoot, "api_luma_image_edit.json"),
                OutputType = "image",
                DefaultModelCode = "uni-1-max",
                CostUnits = 21,
                OutputNodeIds = new[] { "34" },
                InputMappings = new[]
                {
                    new WorkflowInputMapping { Field = "sourceImage", NodeId = "33", InputKey = "image", Required = true },
                    new WorkflowInputMapping { Field = "prompt", NodeId = "37", InputKey = "prompt", Required = true },
                    new WorkflowInputMapping { Field = "seed", NodeId = "37", InputKey = "seed" },
                    new WorkflowInputMapping { Field = "style", NodeId = "37", InputKey = "model.style" },
                    new WorkflowInputMapping { Field = "webSearch", NodeId = "37", InputKey = "model.web_search" }
                }
            },
            new WorkflowDefinition
            {
                WorkflowCode = "api_seedance2",
                Name = "首尾帧漫游视频",
                Description = "上传首帧和尾帧，生成约 7 秒室内空间漫游视频。",
                WorkflowFilePath = Path.Combine(workflowRoot, "api_seedance2.json"),
                OutputType = "video",
                DefaultModelCode = "Seedance 2.0",
                CostUnits = 322,
                OutputNodeIds = new[] { "2" },
                InputMappings = new[]
                {
                    new WorkflowInputMapping { Field = "firstFrame", NodeId = "3", InputKey = "image", Required = true },
                    new WorkflowInputMapping { Field = "lastFrame", NodeId = "4", InputKey = "image", Required = true },
                    new WorkflowInputMapping { Field = "prompt", NodeId = "1", InputKey = "model.prompt" },
                    new WorkflowInputMapping { Field = "seed", NodeId = "1", InputKey = "seed" },
                    new WorkflowInputMapping { Field = "duration", NodeId = "1", InputKey = "model.duration" },
                    new WorkflowInputMapping { Field = "resolution", NodeId = "1", InputKey = "model.resolution" },
                    new WorkflowInputMapping { Field = "aspectRatio", NodeId = "1", InputKey = "model.ratio" },
                    new WorkflowInputMapping { Field = "generateAudio", NodeId = "1", InputKey = "model.generate_audio" }
                }
            },
            new WorkflowDefinition
            {
                WorkflowCode = "api_seedream_image_edit",
                Name = "参考图风格迁移",
                Description = "使用参考图样式编辑原图，偏风格迁移和一致性保持。",
                WorkflowFilePath = Path.Combine(workflowRoot, "api_seedream_image_edit.json"),
                OutputType = "image",
                DefaultModelCode = "seedream 5.0 lite",
                CostUnits = 7,
                OutputNodeIds = new[] { "26" },
                InputMappings = new[]
                {
                    new WorkflowInputMapping { Field = "sourceImage", NodeId = "29", InputKey = "image", Required = true },
                    new WorkflowInputMapping { Field = "referenceImage", NodeId = "32", InputKey = "image", Required = true },
                    new WorkflowInputMapping { Field = "prompt", NodeId = "25", InputKey = "prompt" },
                    new WorkflowInputMapping { Field = "seed", NodeId = "25", InputKey = "seed" },
                    new WorkflowInputMapping { Field = "width", NodeId = "25", InputKey = "width" },
                    new WorkflowInputMapping { Field = "height", NodeId = "25", InputKey = "height" },
                    new WorkflowInputMapping { Field = "batchSize", NodeId = "25", InputKey = "max_images" },
                    new WorkflowInputMapping { Field = "sizePreset", NodeId = "25", InputKey = "size_preset" }
                }
            },
            new WorkflowDefinition
            {
                WorkflowCode = "api_veo3",
                Name = "图片生成漫游视频",
                Description = "基于单张室内图片生成约 6 秒空间漫游视频。",
                WorkflowFilePath = Path.Combine(workflowRoot, "api_veo3.json"),
                OutputType = "video",
                DefaultModelCode = "veo-3.1-generate",
                CostUnits = 343,
                OutputNodeIds = new[] { "10" },
                InputMappings = new[]
                {
                    new WorkflowInputMapping { Field = "sourceImage", NodeId = "11", InputKey = "image", Required = true },
                    new WorkflowInputMapping { Field = "prompt", NodeId = "1", InputKey = "prompt" },
                    new WorkflowInputMapping { Field = "seed", NodeId = "1", InputKey = "seed" },
                    new WorkflowInputMapping { Field = "duration", NodeId = "1", InputKey = "duration_seconds" },
                    new WorkflowInputMapping { Field = "resolution", NodeId = "1", InputKey = "resolution" },
                    new WorkflowInputMapping { Field = "aspectRatio", NodeId = "1", InputKey = "aspect_ratio" },
                    new WorkflowInputMapping { Field = "generateAudio", NodeId = "1", InputKey = "generate_audio" }
                }
            }
        };

        _workflows = definitions.ToDictionary(
            definition => definition.WorkflowCode,
            definition => definition,
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<WorkflowDefinition> GetAll()
    {
        return _workflows.Values.OrderBy(workflow => workflow.CostUnits).ToList();
    }

    public WorkflowDefinition GetRequired(string workflowCode)
    {
        if (TryGet(workflowCode, out var definition))
        {
            return definition;
        }

        throw new AppException(
            ErrorCodes.ValidationError,
            $"工作流不存在或未启用：{workflowCode}",
            StatusCodes.Status400BadRequest);
    }

    public bool TryGet(string workflowCode, out WorkflowDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(workflowCode))
        {
            definition = default!;
            return false;
        }

        return _workflows.TryGetValue(workflowCode.Trim(), out definition!) && definition.Enabled;
    }
}
