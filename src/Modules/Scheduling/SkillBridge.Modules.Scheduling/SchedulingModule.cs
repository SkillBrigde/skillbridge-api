using SkillBridge.BuildingBlocks.Modules;

namespace SkillBridge.Modules.Scheduling;

public sealed class SchedulingModule : ModuleDefinition
{
    public override string Name => "Scheduling";
    protected override string RoutePrefix => "scheduling";
}
