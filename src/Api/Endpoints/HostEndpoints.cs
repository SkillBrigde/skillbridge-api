using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using SkillBridge.BuildingBlocks.Modules;

namespace SkillBridge.Api.Endpoints;

public static class HostEndpoints
{
    public static WebApplication MapApiDocumentation(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference(options => options.WithTitle("SkillBridge Modular API"));
        }

        return app;
    }

    public static WebApplication MapHostEndpoints(this WebApplication app, IReadOnlyCollection<IModule> modules)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live")
        });
        app.MapHealthChecks("/health/ready");

        app.MapGet("/", () => Microsoft.AspNetCore.Http.Results.Ok(new
        {
            Service = "SkillBridge API",
            Architecture = "Modular Monolith (.NET 10)",
            Status = "Running",
            TotalModules = modules.Count,
            ActiveModules = modules.Select(module => module.Name),
            ScalarDocs = "/scalar/v1",
            OpenApiSpec = "/openapi/v1.json",
            Timestamp = DateTimeOffset.UtcNow
        }));

        return app;
    }
}
