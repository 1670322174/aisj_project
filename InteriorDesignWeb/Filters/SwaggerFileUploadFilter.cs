// 作用：修正 Swagger 对文件上传接口的展示方式。
// 当接口参数包含 IFormFile 时，将请求体展示为 multipart/form-data，并保留简单表单字段。

using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace InteriorDesignWeb.Filters;

public class SwaggerFileUploadFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var parameters = context.MethodInfo.GetParameters();
        var hasFileParameter = parameters.Any(parameter =>
            parameter.ParameterType == typeof(IFormFile) ||
            parameter.ParameterType == typeof(IEnumerable<IFormFile>) ||
            parameter.ParameterType == typeof(List<IFormFile>));

        if (!hasFileParameter)
        {
            return;
        }

        var properties = new Dictionary<string, OpenApiSchema>();
        var required = new HashSet<string>();

        foreach (var parameter in parameters)
        {
            if (parameter.ParameterType == typeof(IFormFile))
            {
                properties[parameter.Name ?? "file"] = new OpenApiSchema
                {
                    Type = "string",
                    Format = "binary"
                };
                required.Add(parameter.Name ?? "file");
                continue;
            }

            if (parameter.ParameterType == typeof(string))
            {
                properties[parameter.Name ?? "value"] = new OpenApiSchema { Type = "string" };
                continue;
            }

            if (parameter.ParameterType == typeof(bool))
            {
                properties[parameter.Name ?? "value"] = new OpenApiSchema { Type = "boolean" };
                continue;
            }
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
                        Properties = properties,
                        Required = required
                    }
                }
            }
        };
    }
}
