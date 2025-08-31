using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.Json.Serialization;

namespace EmailVerificationAPI.Swagger
{
    public class HidePropertySchemaFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (schema?.Properties == null || context.Type == null)
                return;

            var ignoredProperties = context.Type
                .GetProperties()
                .Where(prop => prop.GetCustomAttributes(typeof(JsonIgnoreAttribute), true).Any())
                .Select(prop => prop.Name);

            foreach (var prop in ignoredProperties)
            {
                if (schema.Properties.ContainsKey(prop))
                    schema.Properties.Remove(prop);
            }
        }
    }
}
