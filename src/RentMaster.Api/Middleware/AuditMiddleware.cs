using System.Diagnostics;
using System.Security.Claims;
using RentMaster.Domain.Entities;
using RentMaster.Infrastructure.Persistence;

namespace RentMaster.Api.Middleware;

public sealed class AuditMiddleware(
    RequestDelegate next,
    IServiceScopeFactory scopeFactory)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        await next(context);
        stopwatch.Stop();

        if (context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path.StartsWithSegments("/openapi"))
        {
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            dbContext.AuditLogs.Add(new AuditLog
            {
                OccurredAtUtc = DateTimeOffset.UtcNow,
                UserId = context.User.FindFirstValue(ClaimTypes.NameIdentifier),
                HttpMethod = context.Request.Method,
                Path = context.Request.Path.Value ?? "/",
                StatusCode = context.Response.StatusCode,
                DurationMs = stopwatch.ElapsedMilliseconds,
                IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                UserAgent = context.Request.Headers.UserAgent.ToString(),
                TraceId = context.TraceIdentifier
            });

            await dbContext.SaveChangesAsync();
        }
        catch
        {
            // Audit persistence must not replace the real API response.
            // Production should also send audit failures to centralized logging/alerts.
        }
    }
}
