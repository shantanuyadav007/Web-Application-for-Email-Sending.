using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public class FormFileOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var fileParams = context.MethodInfo.GetParameters()
            .Where(p => p.ParameterType == typeof(IFormFile) || p.ParameterType == typeof(List<IFormFile>))
            .ToList();

        if (!fileParams.Any())
            return;

        // Remove existing parameters that are file parameters to avoid duplication
        foreach (var param in fileParams)
        {
            var toRemove = operation.Parameters.SingleOrDefault(p => p.Name == param.Name);
            if (toRemove != null)
            {
                operation.Parameters.Remove(toRemove);
            }
        }

        // Setup request body schema for multipart/form-data with file and other form parameters
        operation.RequestBody = new OpenApiRequestBody
        {
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = GenerateSchema(context, fileParams)
                }
            }
        };
    }

    private OpenApiSchema GenerateSchema(OperationFilterContext context, List<ParameterInfo> fileParams)
    {
        var schema = new OpenApiSchema
        {
            Type = "object",
            Properties = new Dictionary<string, OpenApiSchema>(),
            Required = new HashSet<string>()
        };

        // Add all form parameters from action except those already handled
        var parameters = context.ApiDescription.ParameterDescriptions;

        foreach (var param in parameters)
        {
            if (param.Source.Id == "Form" && !fileParams.Any(f => f.Name == param.Name))
            {
                schema.Properties.Add(param.Name, new OpenApiSchema { Type = "string" });
                schema.Required.Add(param.Name);
            }
        }

        // Add file parameters
        foreach (var param in fileParams)
        {
            schema.Properties.Add(param.Name ?? "file", new OpenApiSchema
            {
                Type = "string",
                Format = "binary"
            });
            schema.Required.Add(param.Name ?? "file");
        }

        return schema;
    }
}
