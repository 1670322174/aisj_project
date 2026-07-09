// 作用：定义工作流注册表接口。
// 通过 workflowCode 查询工作流定义，供统一 AI 生图入口和 Swagger 选项接口使用。

namespace InteriorDesignWeb.Services.AI;

public interface IWorkflowRegistry
{
    IReadOnlyList<WorkflowDefinition> GetAll();

    WorkflowDefinition GetRequired(string workflowCode);

    bool TryGet(string workflowCode, out WorkflowDefinition definition);
}
