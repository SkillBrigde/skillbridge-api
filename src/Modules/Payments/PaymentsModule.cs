using SkillBridge.BuildingBlocks.Modules;

namespace SkillBridge.Modules.Payments;

public sealed class PaymentsModule : ModuleDefinition
{
    public override string Name => "Payments";
    public override string RoutePrefix => "payments";
}
