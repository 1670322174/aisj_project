// 作用：定义 AI 生成任务的统一状态，避免在业务代码中散落硬编码字符串。
// 旧 /api/flux 可继续使用 processing/completed，新 /api/ai/generations 使用下列标准状态。

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
