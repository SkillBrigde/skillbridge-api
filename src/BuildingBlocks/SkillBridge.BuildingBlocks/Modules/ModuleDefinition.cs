using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SkillBridge.BuildingBlocks.Modules;

public abstract class ModuleDefinition : IModule
{
    public abstract string Name { get; }

    protected abstract string RoutePrefix { get; }

    public virtual void AddServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public virtual void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup($"/api/v1/{RoutePrefix}").WithTags(Name);

        group.MapGet("/_module", () => Results.Ok(new ModuleMetadata(
            Name,
            RoutePrefix,
            "available")));
    }

    private sealed record ModuleMetadata(string Name, string RoutePrefix, string Status);
}
