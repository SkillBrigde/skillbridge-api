using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SkillBridge.BuildingBlocks.Contracts;

public interface IModule
{
    string Name { get; }
    string RoutePrefix { get; }
    void AddServices(IServiceCollection services, IConfiguration configuration);
    void MapEndpoints(IEndpointRouteBuilder endpoints);
    Task InitializeAsync(IServiceProvider serviceProvider) => Task.CompletedTask;
}
