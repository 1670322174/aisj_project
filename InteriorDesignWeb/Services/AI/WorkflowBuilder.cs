// 作用：根据 WorkflowRegistry 的节点映射，把用户输入写入 ComfyUI API 工作流 JSON。
// 这样接入 7 个工作流时只维护映射表，不在业务代码里硬编码节点处理逻辑。

using System.Text.Json;
using InteriorDesignWeb.Application.Common;
using InteriorDesignWeb.Models.DTOs.AI;
using Newtonsoft.Json.Linq;
using Microsoft.AspNetCore.Http;

namespace InteriorDesignWeb.Services.AI;

public class WorkflowBuilder : IWorkflowBuilder
{
    private readonly ILogger<WorkflowBuilder> _logger;

    public WorkflowBuilder(ILogger<WorkflowBuilder> logger)
    {
        _logger = logger;
    }

    public JObject Build(WorkflowDefinition definition, AIGenerationSubmitRequest request)
    {
        if (!File.Exists(definition.WorkflowFilePath))
        {
            throw new AppException(
                ErrorCodes.ValidationError,
                $"工作流文件不存在：{definition.WorkflowFilePath}",
                StatusCodes.Status400BadRequest);
        }

        var workflowJson = File.ReadAllText(definition.WorkflowFilePath);
        var workflow = JObject.Parse(workflowJson);

        var values = BuildValueBag(request);

        foreach (var mapping in definition.InputMappings)
        {
            if (!values.TryGetValue(mapping.Field, out var value) || IsEmpty(value))
            {
                if (mapping.Required)
                {
                    throw new AppException(
                        ErrorCodes.ValidationError,
                        $"工作流 {definition.WorkflowCode} 缺少必要输入：{mapping.Field}",
                        StatusCodes.Status400BadRequest);
                }

                if (mapping.DefaultValue is null)
                {
                    continue;
                }

                value = mapping.DefaultValue;
            }

            UpdateNodeInput(workflow, mapping, value);
        }

        _logger.LogInformation(
            "工作流构建完成。WorkflowCode={WorkflowCode}, File={WorkflowFile}",
            definition.WorkflowCode,
            definition.WorkflowFilePath);

        return workflow;
    }

    private static Dictionary<string, object?> BuildValueBag(AIGenerationSubmitRequest request)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["workflowCode"] = request.WorkflowCode,
            ["modelCode"] = request.ModelCode,
            ["prompt"] = request.Prompt,
            ["negativePrompt"] = request.NegativePrompt,
            ["sourceImage"] = request.SourceImageName,
            ["referenceImage"] = request.ReferenceImageName,
            ["firstFrame"] = request.FirstFrameImageName,
            ["lastFrame"] = request.LastFrameImageName
        };

        if (request.InputImages != null)
        {
            foreach (var item in request.InputImages)
            {
                values[item.Key] = item.Value;
            }
        }

        if (request.Parameters != null)
        {
            foreach (var item in request.Parameters)
            {
                values[item.Key] = NormalizeJsonValue(item.Value);
            }
        }

        return values;
    }

    private static object? NormalizeJsonValue(object? value)
    {
        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
                JsonValueKind.Number when element.TryGetDouble(out var doubleValue) => doubleValue,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => element.ToString()
            };
        }

        return value;
    }

    private static bool IsEmpty(object? value)
    {
        return value == null || value is string text && string.IsNullOrWhiteSpace(text);
    }

    private static void UpdateNodeInput(JObject workflow, WorkflowInputMapping mapping, object? value)
    {
        var node = workflow[mapping.NodeId] as JObject;
        if (node == null)
        {
            throw new AppException(
                ErrorCodes.ValidationError,
                $"工作流节点不存在：{mapping.NodeId}",
                StatusCodes.Status400BadRequest);
        }

        var inputs = node["inputs"] as JObject;
        if (inputs == null)
        {
            throw new AppException(
                ErrorCodes.ValidationError,
                $"工作流节点 {mapping.NodeId} 缺少 inputs。",
                StatusCodes.Status400BadRequest);
        }

        inputs[mapping.InputKey] = value == null
            ? JValue.CreateNull()
            : JToken.FromObject(value);
    }
}
