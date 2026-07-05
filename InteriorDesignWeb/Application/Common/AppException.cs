namespace InteriorDesignWeb.Application.Common;

public class AppException : Exception
{
    public string Code { get; }
    public int StatusCode { get; }

    public AppException(string code, string message, int statusCode = StatusCodes.Status400BadRequest)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public static AppException NotFound(string message = "资源不存在")
        => new(ErrorCodes.NotFound, message, StatusCodes.Status404NotFound);

    public static AppException Forbidden(string message = "没有权限访问该资源")
        => new(ErrorCodes.Forbidden, message, StatusCodes.Status403Forbidden);

    public static AppException Validation(string message = "请求参数不正确")
        => new(ErrorCodes.ValidationError, message, StatusCodes.Status400BadRequest);

    public static AppException QuotaExceeded(string message = "额度不足")
        => new(ErrorCodes.QuotaExceeded, message, StatusCodes.Status409Conflict);
}
