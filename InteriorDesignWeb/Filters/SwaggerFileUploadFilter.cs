// 作用：修正 Swagger 对文件上传接口的展示方式。
// 当接口参数包含 IFormFile 时，将请求体展示为 multipart/form-data。

using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace InteriorDesignWeb.Filters;

public class SwaggerFileUploadFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var hasFileParameter = context.MethodInfo
            .GetParameters()
            .Any(parameter =>
                parameter.ParameterType == typeof(IFormFile) ||
                parameter.ParameterType == typeof(IEnumerable<IFormFile>) ||
                parameter.ParameterType == typeof(List<IFormFile>));

        if (!hasFileParameter)
        {
            return;
        }

        operation.Parameters.Clear();
        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, OpenApiSchema>
                        {
                            ["file"] = new OpenApiSchema
                            {
                                Type = "string",
                                Format = "binary"
                            }
                        },
                        Required = new HashSet<string> { "file" }
                    }
                }
            }
        };
    }
}
