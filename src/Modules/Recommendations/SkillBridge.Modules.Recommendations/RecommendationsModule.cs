
using SkillBridge.BuildingBlocks.Contracts;

namespace SkillBridge.Modules.Recommendations;

public sealed class RecommendationsModule : ModuleDefinition
{
    public override string Name => "Recommendations";
    public override string RoutePrefix => "recommendations";
}
