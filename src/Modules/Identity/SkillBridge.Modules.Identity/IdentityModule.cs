using SkillBridge.BuildingBlocks.Modules;

namespace SkillBridge.Modules.Identity;

public sealed class IdentityModule : ModuleDefinition
{
    public override string Name => "Identity";
    protected override string RoutePrefix => "identity";
}
