using Microsoft.AspNetCore.Diagnostics;

namespace SkillBridge.Api.Middleware;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var status = exception is BadHttpRequestException badRequest
            ? badRequest.StatusCode
            : StatusCodes.Status500InternalServerError;

        if (status >= 500)
        {
            logger.LogError(exception, "Request {CorrelationId} failed.", httpContext.TraceIdentifier);
        }

        await Results.Problem(
            statusCode: status,
            title: status >= 500 ? "Server Error" : "Invalid Request",
            detail: status >= 500
                ? "Đã xảy ra lỗi nội bộ máy chủ. Vui lòng thử lại sau."
                : "Nội dung yêu cầu không hợp lệ.",
            instance: httpContext.Request.Path,
            extensions: new Dictionary<string, object?> { ["correlationId"] = httpContext.TraceIdentifier })
            .ExecuteAsync(httpContext);

        return true;
    }
}
