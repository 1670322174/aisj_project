namespace InteriorDesignWeb.Application.Common;

public static class ErrorCodes
{
    public const string OK = "OK";

    public const string ValidationError = "VALIDATION_ERROR";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string Conflict = "CONFLICT";
    public const string ServerError = "SERVER_ERROR";

    public const string QuotaExceeded = "QUOTA_EXCEEDED";

    public const string UploadFileInvalid = "UPLOAD_FILE_INVALID";
    public const string StorageError = "STORAGE_ERROR";

    public const string ProjectNotFound = "PROJECT_NOT_FOUND";
    public const string ProjectAccessDenied = "PROJECT_ACCESS_DENIED";
    public const string RoomNotFound = "ROOM_NOT_FOUND";
    public const string ImageNotFound = "IMAGE_NOT_FOUND";

    public const string AiJobNotFound = "AI_JOB_NOT_FOUND";
    public const string AiJobAccessDenied = "AI_JOB_ACCESS_DENIED";
    public const string AiProviderError = "AI_PROVIDER_ERROR";
    public const string AiJobTimeout = "AI_JOB_TIMEOUT";
}
