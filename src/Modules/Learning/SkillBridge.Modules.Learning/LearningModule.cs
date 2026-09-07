using SkillBridge.BuildingBlocks.Modules;

namespace SkillBridge.Modules.Learning;

public sealed class LearningModule : ModuleDefinition
{
    public override string Name => "Learning";
    protected override string RoutePrefix => "learning";
}
