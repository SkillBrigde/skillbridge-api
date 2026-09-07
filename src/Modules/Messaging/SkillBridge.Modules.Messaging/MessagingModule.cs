using SkillBridge.BuildingBlocks.Modules;

namespace SkillBridge.Modules.Messaging;

public sealed class MessagingModule : ModuleDefinition
{
    public override string Name => "Messaging";
    protected override string RoutePrefix => "messaging";
}
