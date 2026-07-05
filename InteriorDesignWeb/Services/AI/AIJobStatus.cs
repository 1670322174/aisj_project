// 作用：定义 AI 生成任务的统一状态，避免在业务代码中散落硬编码字符串。
// 当前阶段只作为任务中心状态常量，不直接绑定具体的 7 个工作流。

namespace InteriorDesignWeb.Services.AI;

public static class AIJobStatus
{
    public const string Created = "created";
    public const string Processing = "processing";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
}
