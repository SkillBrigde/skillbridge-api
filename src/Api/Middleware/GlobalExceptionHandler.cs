using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace SkillBridge.Api.Middleware;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is FluentValidation.ValidationException validationException)
        {
            _logger.LogWarning(validationException, "Lỗi xác thực dữ liệu đầu vào: {Count} lỗi", validationException.Errors.Count());

            var validationProblem = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1",
                Title = "Validation Failed",
                Detail = "Một hoặc nhiều trường dữ liệu không hợp lệ.",
                Instance = httpContext.Request.Path
            };

            var errors = validationException.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray());

            validationProblem.Extensions["errors"] = errors;

            if (httpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationVal))
            {
                validationProblem.Extensions["correlationId"] = correlationVal.ToString();
            }

            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(validationProblem, cancellationToken);
            return true;
        }

        _logger.LogError(exception, "Đã xảy ra lỗi không mong muốn: {Message}", exception.Message);

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.1",
            Title = "Server Error",
            Detail = "Đã xảy ra lỗi nội bộ máy chủ. Vui lòng thử lại sau.",
            Instance = httpContext.Request.Path
        };

        // Đính kèm Correlation ID vào ProblemDetails để tiện debug
        if (httpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationId))
        {
            problemDetails.Extensions["correlationId"] = correlationId.ToString();
        }

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
