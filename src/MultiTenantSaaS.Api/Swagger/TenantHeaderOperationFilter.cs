using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MultiTenantSaaS.Api.Swagger;

/// <summary>
/// Documents the X-Tenant header on anonymous endpoints only. For authenticated requests the
/// organization comes from the token's tenant_id claim, which outranks any header. Without
/// this filter, anyone trying to log in from Swagger gets a 400 with no clue why.
/// </summary>
public sealed class TenantHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;

        var isAnonymous = metadata.OfType<AllowAnonymousAttribute>().Any();
        var isRegistration = context.ApiDescription.RelativePath?
            .Contains("tenants/register", StringComparison.OrdinalIgnoreCase) == true;

        // Registration creates the organization, so by definition it has no tenant yet.
        if (!isAnonymous || isRegistration)
        {
            return;
        }

        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Tenant",
            In = ParameterLocation.Header,
            Required = true,
            Description = "Organization slug, for example: acme. Required only for unauthenticated requests.",
            Schema = new OpenApiSchema { Type = "string", Example = new OpenApiString("acme") }
        });
    }
}
