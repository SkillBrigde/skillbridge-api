
using SkillBridge.BuildingBlocks.Contracts;

namespace SkillBridge.Modules.Profiles;

public sealed class ProfilesModule : ModuleDefinition
{
    public override string Name => "Profiles";
    public override string RoutePrefix => "profiles";
}
