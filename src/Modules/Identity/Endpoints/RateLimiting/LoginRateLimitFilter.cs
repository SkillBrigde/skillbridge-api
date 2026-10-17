using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkillBridge.BuildingBlocks.RateLimiting;
using StackExchange.Redis;

namespace SkillBridge.Modules.Identity.Endpoints.RateLimiting;

public sealed class LoginRateLimitFilter(
    IOptions<LoginRateLimitOptions> options,
    IHostEnvironment environment,
    ILogger<LoginRateLimitFilter> logger) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var ipAddress = httpContext.Connection.RemoteIpAddress;

        if (ipAddress is null)
        {
            return Unavailable();
        }

        if (ipAddress.IsIPv4MappedToIPv6)
        {
            ipAddress = ipAddress.MapToIPv4();
        }

        var settings = options.Value;
        var key =
            $"skillbridge:{environment.EnvironmentName}:rate-limit:login:ip:{ipAddress}";

        RateLimitDecision decision;

        try
        {
            var store = httpContext.RequestServices
                .GetRequiredService<IRateLimitStore>();

            decision = await store.TryAcquireAsync(
                key,
                settings.PermitLimit,
                TimeSpan.FromSeconds(settings.WindowSeconds),
                httpContext.RequestAborted);
        }
        catch (Exception exception)
            when (exception is RedisException or TimeoutException)
        {
            logger.LogWarning(
                "Login rate limit unavailable: {FailureType}",
                exception.GetType().Name);

            return Unavailable();
        }

        if (!decision.IsAllowed)
        {
            httpContext.Response.Headers.RetryAfter =
                Math.Max(1, decision.RetryAfterSeconds)
                    .ToString(CultureInfo.InvariantCulture);

            return Results.Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Bạn đã gửi quá nhiều yêu cầu đăng nhập.",
                detail: "Vui lòng thử lại sau thời gian trong Retry-After.");
        }

        return await next(context);
    }

    private static IResult Unavailable() =>
        Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Dịch vụ đăng nhập tạm thời không khả dụng.");
}