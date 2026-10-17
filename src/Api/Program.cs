using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using SkillBridge.Api.Common;
using SkillBridge.Api.Middleware;
using SkillBridge.Api.Security;
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

// Nạp .env trước khi tạo host.
DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

// Chuẩn hóa lỗi API.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// OpenAPI và Scalar.
builder.Services.AddOpenApi();

// CORS.
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

// Health checks.
builder.Services
    .AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);

// Các module nghiệp vụ.
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

// Hạ tầng dùng chung và xác thực HTTP.
var moduleAssemblies = modules.Select(m => m.GetType().Assembly).Distinct().ToArray();
builder.Services.AddBuildingBlocks(builder.Configuration, moduleAssemblies);
builder.Services.AddSecurityServices(builder.Configuration);
builder.Services.AddApiAuthentication();

// Đăng ký dịch vụ của từng module.
foreach (var module in modules)
{
    module.AddServices(builder.Services, builder.Configuration);
}

var app = builder.Build();

// HTTP pipeline.
app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseCors("Default");
app.UseAuthentication();
app.UseAuthorization();

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
app.MapHealthChecks("/health/ready");

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
