using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentMaster.Application.Common;

namespace RentMaster.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            if (context.Response.HasStarted)
            {
                logger.LogError(exception,
                    "Unhandled error after the response started. TraceId: {TraceId}",
                    context.TraceIdentifier);
                throw;
            }

            await HandleAsync(context, exception);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var (status, title, detail) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed", exception.Message),
            UnauthorizedException => (StatusCodes.Status401Unauthorized, "Unauthorized", exception.Message),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden", exception.Message),
            NotFoundException => (StatusCodes.Status404NotFound, "Not found", exception.Message),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict", exception.Message),
            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "Concurrency conflict",
                "The record was modified by another request."),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Unexpected error",
                "Something went wrong.")
        };

        if (status >= 500)
            logger.LogError(exception, "Unhandled error. TraceId: {TraceId}", context.TraceIdentifier);
        else
            logger.LogWarning(exception,
                "Request failed with {StatusCode}. TraceId: {TraceId}",
                status,
                context.TraceIdentifier);

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;

        await context.Response.WriteAsJsonAsync(problem, context.RequestAborted);
    }
}
