using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Hrms.Api;

public class SwaggerHeader : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Get controller and action names
        var controllerName = context.ApiDescription.ActionDescriptor.RouteValues["controller"];
        var actionName = context.ApiDescription.ActionDescriptor.RouteValues["action"];

        // Define exclusions
        var excludedRoutes = new[]
        {
            ("Tenants", "Register"),
            ("Tenants", "Post"),
            ("Tenants", "Get"),
            ("Tenants", "Put"),
            ("Tenants", "Delete"),
        };

        // Skip header injection for excluded routes
        if (excludedRoutes.Any(r => r.Item1 == controllerName && r.Item2 == actionName))
            return;

        operation.Parameters ??= new List<OpenApiParameter>();

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Tenant-ID",
            In = ParameterLocation.Header,
            Required = true,
            Schema = new OpenApiSchema
            {
                Type = "string",
                Default = new OpenApiString("08de7dde-6375-4e2b-898a-a51da793ad2a")
            }
        });
    }
}