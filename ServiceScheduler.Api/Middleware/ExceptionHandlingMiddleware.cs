using Microsoft.AspNetCore.Mvc;
using ServiceScheduler.Api.Contracts;
using ServiceScheduler.Domain.Exceptions;

namespace ServiceScheduler.Api.Middleware;

internal static class Problems
{
    public static ProblemDetails Create(HttpContext context, int status, string title, string detail, string code) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Extensions =
        {
            ["code"] = code,
            ["correlationId"] = CorrelationId.Get(context)
        }
    };

    /// <summary>Framework-detected body problems (malformed JSON, wrong types) use the same shape as every other error.</summary>
    public static IActionResult InvalidModelState(ActionContext context) =>
        new ObjectResult(Create(context.HttpContext, StatusCodes.Status400BadRequest, "Bad request",
            "The request body is missing or is not valid.", "bad_request"))
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { "application/problem+json" }
        };
}

/// <summary>Turns exceptions into ProblemDetails responses. Nothing but the correlation id identifies an unexpected failure to the client.</summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499; // client closed the request; nobody is left to read a body
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            var (status, title, code, detail) = Map(ex);

            if (status >= StatusCodes.Status500InternalServerError)
            {
                logger.LogError(ex, "Unhandled exception while processing {Method} {Path}", context.Request.Method, context.Request.Path);
            }
            else
            {
                logger.LogInformation("Request rejected: {Code} ({ExceptionType})", code, ex.GetType().Name);
            }

            context.Response.Clear();
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(
                Problems.Create(context, status, title, detail, code), options: null, contentType: "application/problem+json");
        }
    }

    private static (int Status, string Title, string Code, string Detail) Map(Exception ex) => ex switch
    {
        BadRequestException => (400, "Bad request", "bad_request", ex.Message),
        DomainValidationException => (422, "Validation failed", "validation_failed", ex.Message),
        IdempotencyConflictException => (422, "Idempotency key reused", "idempotency_key_reused", ex.Message),
        EntityNotFoundException => (404, "Not found", "not_found", ex.Message),
        ResourceUnavailableException => (409, "Slot not available", "resource_unavailable", ex.Message),
        InvalidStateTransitionException => (409, "Invalid state transition", "invalid_state_transition", ex.Message),
        DuplicateIdempotencyKeyException => (409, "Duplicate request", "duplicate_request", ex.Message),
        _ => (500, "An unexpected error occurred", "internal_error",
            "The request could not be completed. Quote the correlation id when reporting this.")
    };
}
