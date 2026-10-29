namespace ServiceScheduler.Api.Middleware;

public static class CorrelationId
{
    public const string HeaderName = "X-Correlation-Id";
    private const string ItemKey = "ServiceScheduler.CorrelationId";
    private const int MaxLength = 128;

    internal static void Set(HttpContext context, string value) => context.Items[ItemKey] = value;

    public static string Get(HttpContext context) =>
        context.Items.TryGetValue(ItemKey, out var value) && value is string id ? id : context.TraceIdentifier;

    internal static string Resolve(HttpContext context)
    {
        var supplied = context.Request.Headers[HeaderName].FirstOrDefault();
        return string.IsNullOrWhiteSpace(supplied) || supplied.Length > MaxLength ? Guid.NewGuid().ToString("N") : supplied;
    }
}

/// <summary>Uses the caller's X-Correlation-Id or makes one up, echoes it on the response and puts it on every log line.</summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var id = CorrelationId.Resolve(context);
        CorrelationId.Set(context, id);
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationId.HeaderName] = id;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = id }))
        {
            await next(context);
        }
    }
}
