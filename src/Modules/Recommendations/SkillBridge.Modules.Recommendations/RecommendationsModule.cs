using SkillBridge.BuildingBlocks.Modules;

namespace SkillBridge.Modules.Recommendations;

public sealed class RecommendationsModule : ModuleDefinition
{
    public override string Name => "Recommendations";
    protected override string RoutePrefix => "recommendations";
}
