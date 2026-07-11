// 作用：定义 AI 生成任务的统一状态，避免在业务代码中散落硬编码字符串。
// 所有 AI 接口统一使用本文件中的状态，避免业务状态与 ComfyUI Server 原始状态混用。

namespace InteriorDesignWeb.Services.AI;

public static class AIJobStatus
{
    public const string Created = "created";
    public const string Queued = "queued";
    public const string Processing = "processing";
    public const string Running = "running";
    public const string Uploading = "uploading";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
    public const string Timeout = "timeout";
}
