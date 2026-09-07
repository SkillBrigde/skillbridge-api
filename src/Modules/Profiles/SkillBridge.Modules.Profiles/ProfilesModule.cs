using SkillBridge.BuildingBlocks.Modules;

namespace SkillBridge.Modules.Profiles;

public sealed class ProfilesModule : ModuleDefinition
{
    public override string Name => "Profiles";
    protected override string RoutePrefix => "profiles";
}
