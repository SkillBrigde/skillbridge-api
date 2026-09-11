
using SkillBridge.BuildingBlocks.Contracts;

namespace SkillBridge.Modules.Learning;

public sealed class LearningModule : ModuleDefinition
{
    public override string Name => "Learning";
    public override string RoutePrefix => "learning";
}
