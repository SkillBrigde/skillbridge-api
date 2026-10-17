using Microsoft.Extensions.Primitives;

namespace SkillBridge.Api.Middleware;

public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeaderName = "X-Correlation-ID";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 1. Kiểm tra xem client có gửi kèm Header X-Correlation-ID không, nếu không thì tự sinh mới
        if (!context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out StringValues correlationId)
            || correlationId.Count != 1 || string.IsNullOrWhiteSpace(correlationId)
            || correlationId.ToString().Length > 128
            || correlationId.ToString().Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_' and not '.'))
        {
            correlationId = Guid.NewGuid().ToString("N");
            context.Request.Headers[CorrelationIdHeaderName] = correlationId;
        }

        context.TraceIdentifier = correlationId.ToString();

        // 2. Gán Correlation ID vào Response Header để client đối chiếu khi cần
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeaderName] = correlationId;
            return Task.CompletedTask;
        });

        await _next(context);
    }
}
