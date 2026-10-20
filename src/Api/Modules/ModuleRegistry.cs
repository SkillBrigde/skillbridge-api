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

namespace SkillBridge.Api.Modules;

public static class ModuleRegistry
{
    public static IReadOnlyList<IModule> Create() =>
    [
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

    public static async Task InitializeModulesAsync(this WebApplication app, IEnumerable<IModule> modules)
    {
        foreach (var module in modules)
        {
            await module.InitializeAsync(app.Services);
        }
    }

    public static WebApplication MapModuleEndpoints(this WebApplication app, IEnumerable<IModule> modules)
    {
        foreach (var module in modules)
        {
            module.MapEndpoints(app);
        }

        return app;
    }
}
