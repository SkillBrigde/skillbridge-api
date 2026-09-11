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
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

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
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("SkillBridge Modular API");
    });

    // Má»—i module tá»± khá»Ÿi táº¡o vĂ  Ă¡p dá»¥ng migration Ä‘á»™c láº­p (Encapsulated Initialization)
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
app.MapHealthChecks("/health/ready");

// Root endpoint kiá»ƒm tra tráº¡ng thĂ¡i chung cá»§a Host
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

// ÄÄƒng kĂ½ Endpoints cá»§a tá»«ng Module
foreach (var module in modules)
{
    module.MapEndpoints(app);
}

app.Run();

// Cho phĂ©p WebApplicationFactory trong Integration Tests truy cáº­p
public partial class Program;
