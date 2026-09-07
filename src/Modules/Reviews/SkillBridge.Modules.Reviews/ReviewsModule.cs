using SkillBridge.BuildingBlocks.Modules;

namespace SkillBridge.Modules.Reviews;

public sealed class ReviewsModule : ModuleDefinition
{
    public override string Name => "Reviews";
    protected override string RoutePrefix => "reviews";
}
