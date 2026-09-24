using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using SkillBridge.Api.Common;
using SkillBridge.Api.Middleware;
using SkillBridge.BuildingBlocks.Contracts;
using SkillBridge.BuildingBlocks.Extensions;
using SkillBridge.BuildingBlocks.Security;
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

// 0. Tự động nạp file .env vào Environment Variables trước khi cấu hình WebApplication
DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

// 1. Cấu hình bối cảnh bảo mật & người dùng hiện tại (ICurrentUser)
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// 2. Cấu hình xử lý lỗi tập trung (ProblemDetails & Global Exception Handler)
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Instance = context.HttpContext.Request.Path;
    context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.TraceIdentifier;
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: $"{context.Connection.RemoteIpAddress}:{context.GetEndpoint()?.DisplayName}",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

// 3. Cấu hình OpenAPI (Swagger & Scalar)
builder.Services.AddOpenApi();

// 4. Cấu hình CORS
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
        else
        {
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

// 5. Cấu hình Health Checks
builder.Services
    .AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

// 6. Danh sách toàn bộ 10 Bounded Context Modules trong hệ thống
List<IModule> modules = [
    new IdentityModule(),
    new ProfilesModule(),
    new CatalogModule(),
    new SchedulingModule(),
    new BookingModule(),
    new PaymentsModule(),
    new LearningModule(),
    new MessagingModule(),
    new ReviewsModule(),
    new RecommendationsModule()
];

// 7. Cấu hình Enterprise BuildingBlocks (MediatR, FluentValidation, EventBus, Interceptors)
var moduleAssemblies = modules.Select(m => m.GetType().Assembly).Distinct().ToArray();
builder.Services.AddBuildingBlocks(builder.Configuration, moduleAssemblies);
builder.Services.AddJwtSecurity(builder.Configuration);

// 8. Đăng ký dịch vụ (DI) của từng Module độc lập
foreach (var module in modules)
{
    module.AddServices(builder.Services, builder.Configuration);
}

var app = builder.Build();

// 9. HTTP Request Pipeline
app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseCors("Default");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("SkillBridge Modular API");
    });

    // Mỗi module tự khởi tạo và áp dụng migration độc lập (Encapsulated Initialization)
    foreach (var module in modules)
    {
        await module.InitializeAsync(app.Services);
    }
}

// Health check endpoints
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

// Root endpoint kiểm tra trạng thái chung của Host
app.MapGet("/", () => Microsoft.AspNetCore.Http.Results.Ok(new
{
    Service = "SkillBridge API",
    Architecture = "Modular Monolith (.NET 10)",
    Status = "Running",
    TotalModules = modules.Count,
    ActiveModules = modules.Select(m => m.Name),
    ScalarDocs = "/scalar/v1",
    OpenApiSpec = "/openapi/v1.json",
    Timestamp = DateTimeOffset.UtcNow
}));

// Đăng ký Endpoints của từng Module
foreach (var module in modules)
{
    module.MapEndpoints(app);
}

app.Run();

// Cho phép WebApplicationFactory trong Integration Tests truy cập
public partial class Program;
