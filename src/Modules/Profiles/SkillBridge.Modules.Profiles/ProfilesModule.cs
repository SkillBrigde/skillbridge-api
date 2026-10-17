using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkillBridge.BuildingBlocks.Contracts;
using SkillBridge.Modules.Profiles.Application.Mentors;
using SkillBridge.Modules.Profiles.Endpoints;
using SkillBridge.Modules.Profiles.Infrastructure;

namespace SkillBridge.Modules.Profiles;

public sealed class ProfilesModule : ModuleDefinition
{
    public override string Name => "Profiles";
    public override string RoutePrefix => "profiles";

    public override void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("ConnectionStrings:Database is required.");

        services.AddDbContext<ProfilesDbContext>(options => options.UseNpgsql(connectionString,
            npgsqlOptions => npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", ProfilesDbContext.Schema)));
        services.TryAddScoped<ISkillDirectory, UnavailableSkillDirectory>();
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        base.MapEndpoints(endpoints);
        endpoints.MapGetMentorProfile();
        endpoints.MapGetPublicMentorProfile();
        endpoints.MapSearchPublicMentors();
        endpoints.MapUpdateMentorProfile();
        endpoints.MapAddMentorSkill();
        endpoints.MapRemoveMentorSkill();
        endpoints.MapAddMentorExperience();
        endpoints.MapUpdateMentorExperience();
        endpoints.MapRemoveMentorExperience();
        endpoints.MapAddMentorCertification();
        endpoints.MapRemoveMentorCertification();
        endpoints.MapGetMenteeProfile();
        endpoints.MapUpdateMenteeProfile();
    }

    public override async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ProfilesDbContext>().Database.MigrateAsync();
    }
}
