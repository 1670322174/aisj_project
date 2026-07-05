using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace InteriorDesignWeb.Filters;

public class HttpResponseExceptionFilter : IActionFilter, IOrderedFilter
{
    public int Order => int.MaxValue - 10;

    public void OnActionExecuting(ActionExecutingContext context) { }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        if (context.Exception is not null)
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILogger<HttpResponseExceptionFilter>>();

            logger.LogError(context.Exception, "全局异常捕获");

            context.Result = new ObjectResult(new
            {
                Code = 50000,
                Message = "系统发生未处理异常",
                Detail = context.Exception.Message
            })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };

            context.ExceptionHandled = true;
        }
    }
}
