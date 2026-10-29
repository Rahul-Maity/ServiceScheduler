using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ServiceScheduler.Api.Middleware;

/// <summary>Marks an action as needing the Idempotency-Key header (documented in Swagger; enforced by the Application layer).</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequiresIdempotencyKeyAttribute : Attribute;

/// <summary>The header is read by middleware, not bound as an action parameter, so Swagger UI needs it declared explicitly.</summary>
public sealed class IdempotencyKeyHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (!context.MethodInfo.IsDefined(typeof(RequiresIdempotencyKeyAttribute), inherit: true))
        {
            return;
        }

        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = IdempotencyKeyMiddleware.HeaderName,
            In = ParameterLocation.Header,
            Required = true,
            Description = "Unique per logical request (e.g. a GUID). Retrying with the same key and body returns the original result; the same key with a different body is rejected.",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String }
        });
    }
}
