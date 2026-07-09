// 作用：定义 ComfyUI 工作流构建接口。
// 输入业务请求和工作流定义，输出可直接提交给 ComfyUI /prompt 的 prompt JSON。

using InteriorDesignWeb.Models.DTOs.AI;
using Newtonsoft.Json.Linq;

namespace InteriorDesignWeb.Services.AI;

public interface IWorkflowBuilder
{
    JObject Build(WorkflowDefinition definition, AIGenerationSubmitRequest request);
}
