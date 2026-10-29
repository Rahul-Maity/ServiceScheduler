namespace ServiceScheduler.Api.Middleware;

/// <summary>The Idempotency-Key header of the current request, as extracted by <see cref="IdempotencyKeyMiddleware"/>.</summary>
public interface IIdempotencyKeyAccessor
{
    string? Key { get; }
}

public sealed class IdempotencyKeyAccessor : IIdempotencyKeyAccessor
{
    public string? Key { get; set; }
}

/// <summary>Only extracts the header. Whether it is required is decided by the Application layer's validation.</summary>
public sealed class IdempotencyKeyMiddleware(RequestDelegate next)
{
    public const string HeaderName = "Idempotency-Key";

    public async Task InvokeAsync(HttpContext context, IdempotencyKeyAccessor accessor)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var values) && values.Count > 0)
        {
            accessor.Key = values.ToString();
        }

        await next(context);
    }
}
