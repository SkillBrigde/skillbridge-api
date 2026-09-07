using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SkillBridge.Api.Hubs;
using SkillBridge.Api.Middleware;
using SkillBridge.BuildingBlocks.Modules;
using SkillBridge.Modules.Booking;
using SkillBridge.Modules.Catalog;
using SkillBridge.Modules.Identity;
using SkillBridge.Modules.Learning;
using SkillBridge.Modules.Messaging;
using SkillBridge.Modules.Payments;
using SkillBridge.Modules.Profiles;
using SkillBridge.Modules.Recommendations;
using SkillBridge.Modules.Reviews;
using SkillBridge.Modules.Scheduling;

var builder = WebApplication.CreateBuilder(args);

IModule[] modules =
[
    new IdentityModule(),
    new ProfilesModule(),
    new CatalogModule(),
    new BookingModule(),
    new SchedulingModule(),
    new PaymentsModule(),
    new LearningModule(),
    new MessagingModule(),
    new ReviewsModule(),
    new RecommendationsModule()
];

builder.Services.AddProblemDetails();
builder.Services
    .AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);
builder.Services.AddSignalR();

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("WebClient", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

foreach (var module in modules)
{
    module.AddServices(builder.Services, builder.Configuration);
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseCors("WebClient");
app.UseRateLimiter();

app.MapGet("/", () => Results.Ok(new
{
    service = "SkillBridge API",
    architecture = "Modular Monolith",
    status = "running",
    utc = DateTimeOffset.UtcNow
}));

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready");
app.MapHub<SystemHub>("/hubs/system");

foreach (var module in modules)
{
    module.MapEndpoints(app);
}

app.Run();

public partial class Program;
