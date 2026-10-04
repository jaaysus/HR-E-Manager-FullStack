using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace HrETracker.Utils;

public class AnonymousSecurityOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
            operation.Security = [];
    }
}
