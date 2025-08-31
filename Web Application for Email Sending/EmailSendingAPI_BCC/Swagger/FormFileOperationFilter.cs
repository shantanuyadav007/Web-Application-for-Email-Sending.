using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Linq;

namespace EmailVerificationAPI.Swagger
{
    public class FormFileOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var fileParams = context.MethodInfo
                .GetParameters()
                .Where(p => p.ParameterType.Name.Contains("IFormFile"));

            if (!fileParams.Any()) return;

            operation.RequestBody = new OpenApiRequestBody
            {
                Content =
                {
                    ["multipart/form-data"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties = context.MethodInfo.GetParameters()
                                .SelectMany(p => p.ParameterType.GetProperties())
                                .ToDictionary(
                                    prop => prop.Name,
                                    prop => new OpenApiSchema
                                    {
                                        Type = prop.PropertyType.Name == "IFormFile" ? "string" : "string",
                                        Format = prop.PropertyType.Name == "IFormFile" ? "binary" : null
                                    }
                                ),
                            Required = new HashSet<string>()
                        }
                    }
                }
            };
        }
    }
}
