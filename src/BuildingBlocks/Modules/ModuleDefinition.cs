using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SkillBridge.BuildingBlocks.Modules;

public abstract class ModuleDefinition : IModule
{
    public abstract string Name { get; }
    public abstract string RoutePrefix { get; }

    public virtual void AddServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public virtual void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        if (!endpoints.ServiceProvider.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            return;
        }

        var group = endpoints.MapGroup($"/api/v1/{RoutePrefix}").WithTags(Name);

        // Chỉ định rõ Microsoft.AspNetCore.Http.Results để tránh xung đột namespace SkillBridge.BuildingBlocks.Results
        group.MapGet("/_module", () => Microsoft.AspNetCore.Http.Results.Ok(new ModuleMetadata(
            Name,
            RoutePrefix,
            "available",
            DateTimeOffset.UtcNow)));
    }

    public virtual Task InitializeAsync(IServiceProvider serviceProvider) => Task.CompletedTask;

    private sealed record ModuleMetadata(string Name, string RoutePrefix, string Status, DateTimeOffset Timestamp);
}
