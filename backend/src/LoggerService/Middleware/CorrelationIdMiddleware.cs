using System.Diagnostics;

namespace LoggerService.Middleware;

public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string HeaderKey = "X-Correlation-ID";

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(HeaderKey, out var correlationId) || string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Activity.Current?.Id ?? Guid.NewGuid().ToString();
            context.Request.Headers[HeaderKey] = correlationId;
        }

        context.Response.Headers[HeaderKey] = correlationId;

        context.TraceIdentifier = correlationId!;

        await _next(context);
    }
}