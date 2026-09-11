
using SkillBridge.BuildingBlocks.Contracts;

namespace SkillBridge.Modules.Reviews;

public sealed class ReviewsModule : ModuleDefinition
{
    public override string Name => "Reviews";
    public override string RoutePrefix => "reviews";
}
