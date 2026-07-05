namespace InteriorDesignWeb.Application.Common;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Code { get; set; } = ErrorCodes.OK;
    public string Message { get; set; } = "success";
    public T? Data { get; set; }
    public string? RequestId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static ApiResponse<T> Ok(T? data, string message = "success", string? requestId = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Code = ErrorCodes.OK,
            Message = message,
            Data = data,
            RequestId = requestId
        };
    }

    public static ApiResponse<T> Fail(string code, string message, string? requestId = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Code = code,
            Message = message,
            Data = default,
            RequestId = requestId
        };
    }
}

public class ApiResponse : ApiResponse<object>
{
    public static ApiResponse<object> Ok(string message = "success", string? requestId = null)
    {
        return ApiResponse<object>.Ok(null, message, requestId);
    }

    public static new ApiResponse<object> Fail(string code, string message, string? requestId = null)
    {
        return ApiResponse<object>.Fail(code, message, requestId);
    }
}
